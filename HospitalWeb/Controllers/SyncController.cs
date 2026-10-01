
using DocumentFormat.OpenXml.Spreadsheet;
using HospitalWeb.Data;
using HospitalWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalWeb.Controllers
{
    [ApiController]
    [Route("Sync")]
    public class SyncController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SyncController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // اختبار API
        // =========================================================

        [HttpGet("Test")]
        public IActionResult Test()
        {
            return Ok("Hospital Sync API يعمل بنجاح");
        }

        // =========================================================
        // فحص الأطباء
        // =========================================================

        [HttpGet("CheckDoctors")]
        public async Task<IActionResult> CheckDoctors()
        {
            try
            {
                var doctors = await _context.Doctors
                    .AsNoTracking()
                    .OrderBy(x => x.رقم_الطبيب)
                    .ToListAsync();

                var duplicateNumbers = doctors
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.رقم_الطبيب))
                    .GroupBy(
                        x => x.رقم_الطبيب!.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => new
                    {
                        Number = g.Key,
                        Count = g.Count(),
                        Names = g.Select(x => x.الاسم).ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,

                    totalDoctors =
                        doctors.Count,

                    doctorsWithNumber =
                        doctors.Count(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب)),

                    doctorsWithoutNumber =
                        doctors.Count(x =>
                            string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب)),

                    duplicateNumbersCount =
                        duplicateNumbers.Count,

                    duplicateNumbers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        error = ex.Message,
                        innerError = ex.InnerException?.Message,
                        type = ex.GetType().FullName
                    });
            }
        }

        // =========================================================
        // استقبال المزامنة
        // =========================================================

        [HttpPost("Receive")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Receive(
            [FromBody] List<DoctorSyncModel>? doctors)
        {
            if (doctors == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات المزامنة غير صالحة."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                int added = 0;
                int updated = 0;
                int deleted = 0;

                int duplicateRemoved = 0;

                int rotationsAdded = 0;
                int rotationsUpdated = 0;
                int rotationsDeleted = 0;

                // =================================================
                // السجلات الصالحة
                // =================================================

                var validDoctors =
                    doctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.الرقم))
                        .ToList();

                // =================================================
                // إزالة تكرارات Access
                // =================================================

                var cleanDoctors =
                    validDoctors
                        .GroupBy(
                            x => x.الرقم!.Trim(),
                            StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();

                duplicateRemoved +=
                    validDoctors.Count -
                    cleanDoctors.Count;

                // =================================================
                // أرقام Access
                // =================================================

                var accessDoctorNumbers =
                    cleanDoctors
                        .Select(x => x.الرقم!.Trim())
                        .ToHashSet(
                            StringComparer.OrdinalIgnoreCase);

                // =================================================
                // الأقسام
                // =================================================

                var departments =
                    await EnsureDepartmentsAsync();

                // =================================================
                // الأطباء الموجودون
                // =================================================

                var existingDoctors =
                    await _context.Doctors
                        .Include(x =>
                            x.TrainingRotations)
                        .ToListAsync();

                // =================================================
                // إزالة أطباء Neon المكررين
                // =================================================

                var duplicateDoctors =
                    existingDoctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب))
                        .GroupBy(
                            x => x.رقم_الطبيب!.Trim(),
                            StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g.Skip(1))
                        .ToList();

                foreach (var duplicateDoctor in
                         duplicateDoctors)
                {
                    if (duplicateDoctor.TrainingRotations != null)
                    {
                        foreach (var rotation in
                                 duplicateDoctor.TrainingRotations.ToList())
                        {
                            _context.TrainingRotations
                                .Remove(rotation);

                            rotationsDeleted++;
                        }
                    }

                    _context.Doctors
                        .Remove(duplicateDoctor);

                    duplicateRemoved++;
                    deleted++;
                }

                if (duplicateDoctors.Count > 0)
                {
                    PrepareDatesForPostgreSql();

                    await SaveChangesSafeAsync(
                        "حذف الأطباء المكررين");
                }

                // =================================================
                // إعادة قراءة الأطباء
                // =================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x =>
                            x.TrainingRotations)
                        .ToListAsync();

                // =================================================
                // حذف الموجود في Neon وغير الموجود في Access
                // =================================================

                var doctorsToDelete =
                    existingDoctors
                        .Where(x =>
                            string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب)
                            ||
                            !accessDoctorNumbers.Contains(
                                x.رقم_الطبيب!.Trim()))
                        .ToList();

                foreach (var doctor in
                         doctorsToDelete)
                {
                    if (doctor.TrainingRotations != null)
                    {
                        foreach (var rotation in
                                 doctor.TrainingRotations.ToList())
                        {
                            _context.TrainingRotations
                                .Remove(rotation);

                            rotationsDeleted++;
                        }
                    }

                    _context.Doctors
                        .Remove(doctor);

                    deleted++;
                }

                if (doctorsToDelete.Count > 0)
                {
                    PrepareDatesForPostgreSql();

                    await SaveChangesSafeAsync(
                        "حذف الأطباء غير الموجودين في Access");
                }

                // =================================================
                // إعادة القراءة
                // =================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x =>
                            x.TrainingRotations)
                        .ToListAsync();

                // =================================================
                // Dictionary
                // =================================================

                var doctorDictionary =
                    existingDoctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب))
                        .GroupBy(
                            x => x.رقم_الطبيب!.Trim(),
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            g => g.First(),
                            StringComparer.OrdinalIgnoreCase);

                // =================================================
                // إضافة / تحديث الأطباء
                // =================================================

                foreach (var source in cleanDoctors)
                {
                    string doctorNumber =
                        source.الرقم!.Trim();

                    DateTime? startDate =
                        ToUtcDate(
                            source.تاريخ_المباشرة);

                    doctorDictionary.TryGetValue(
                        doctorNumber,
                        out Doctor? doctor);

                    // =================================================
                    // إضافة
                    // =================================================

                    if (doctor == null)
                    {
                        doctor =
                            new Doctor
                            {
                                رقم_الطبيب =
                                    doctorNumber,

                                الاسم =
                                    source.الاسم ?? "",

                                مكان_المباشرة =
                                    source.مكان_المباشرة,

                                تاريخ_المباشرة =
                                    startDate,

                                Phone =
                                    source.Phone,

                                ImagePath =
                                    source.ImagePath
                            };

                        _context.Doctors.Add(doctor);

                        doctorDictionary.Add(
                            doctorNumber,
                            doctor);

                        added++;
                    }

                    // =================================================
                    // تحديث
                    // =================================================

                    else
                    {
                        bool changed = false;

                        string newName =
                            source.الاسم ?? "";

                        if (doctor.الاسم != newName)
                        {
                            doctor.الاسم =
                                newName;

                            changed = true;
                        }

                        if (doctor.مكان_المباشرة !=
                            source.مكان_المباشرة)
                        {
                            doctor.مكان_المباشرة =
                                source.مكان_المباشرة;

                            changed = true;
                        }

                        if (doctor.تاريخ_المباشرة !=
                            startDate)
                        {
                            doctor.تاريخ_المباشرة =
                                startDate;

                            changed = true;
                        }

                        if (doctor.Phone !=
                            source.Phone)
                        {
                            doctor.Phone =
                                source.Phone;

                            changed = true;
                        }

                        if (doctor.ImagePath !=
                            source.ImagePath)
                        {
                            doctor.ImagePath =
                                source.ImagePath;

                            changed = true;
                        }

                        if (changed)
                        {
                            updated++;
                        }
                    }
                }

                // =================================================
                // تجهيز التواريخ
                // =================================================

                PrepareDatesForPostgreSql();

                // =================================================
                // حفظ الأطباء
                // =================================================

                await SaveChangesSafeAsync(
                    "إضافة وتحديث الأطباء");

                // =================================================
                // إعادة قراءة الأطباء
                // =================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x =>
                            x.TrainingRotations)
                        .ToListAsync();

                doctorDictionary =
                    existingDoctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب))
                        .GroupBy(
                            x => x.رقم_الطبيب!.Trim(),
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            g => g.First(),
                            StringComparer.OrdinalIgnoreCase);

                // =================================================
                // مزامنة التدريب
                // =================================================

                foreach (var source in cleanDoctors)
                {
                    string doctorNumber =
                        source.الرقم!.Trim();

                    if (!doctorDictionary.TryGetValue(
                        doctorNumber,
                        out var doctor))
                    {
                        continue;
                    }

                    // ------------------------------------------------
                    // الجراحة
                    // ------------------------------------------------

                    SyncRotation(
                        doctor,
                        "الجراحة",
                        source.الجراحة_مباشرة,
                        source.الجراحة_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    // ------------------------------------------------
                    // الباطني
                    // ------------------------------------------------

                    SyncRotation(
                        doctor,
                        "الباطني",
                        source.الباطني_مباشرة,
                        source.الباطني_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    // ------------------------------------------------
                    // النسائية
                    // ------------------------------------------------

                    SyncRotation(
                        doctor,
                        "النسائية",
                        source.النسائية_مباشرة,
                        source.النسائية_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    // ------------------------------------------------
                    // الأطفال
                    // ------------------------------------------------

                    SyncRotation(
                        doctor,
                        "الأطفال",
                        source.الاطفال_مباشرة,
                        source.الاطفال_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    // ------------------------------------------------
                    // الطوارئ
                    // ------------------------------------------------

                    SyncRotation(
                        doctor,
                        "الطوارئ",
                        source.الطوارئ_مباشرة,
                        source.الطوارئ_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    // ------------------------------------------------
                    // الاختياري
                    // ------------------------------------------------

                    SyncRotation(
                        doctor,
                        "الاختياري",
                        source.الاختياري_مباشرة,
                        source.الاختياري_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);
                }

                // =================================================
                // تجهيز التواريخ
                // =================================================

                PrepareDatesForPostgreSql();

                // =================================================
                // حفظ التدريب
                // =================================================

                await SaveChangesSafeAsync(
                    "مزامنة دورات التدريب");

                // =================================================
                // فحص التكرارات النهائية
                // =================================================

                var allRotations =
                    await _context.TrainingRotations
                        .ToListAsync();

                var duplicateRotations =
                    allRotations
                        .GroupBy(x => new
                        {
                            x.DoctorId,
                            x.DepartmentId
                        })
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g.Skip(1))
                        .ToList();

                foreach (var rotation in
                         duplicateRotations)
                {
                    _context.TrainingRotations
                        .Remove(rotation);

                    duplicateRemoved++;
                    rotationsDeleted++;
                }

                if (duplicateRotations.Count > 0)
                {
                    PrepareDatesForPostgreSql();

                    await SaveChangesSafeAsync(
                        "حذف دورات التدريب المكررة");
                }

                // =================================================
                // النتائج النهائية
                // =================================================

                int finalDoctors =
                    await _context.Doctors.CountAsync();

                int finalDoctorsWithNumber =
                    await _context.Doctors.CountAsync(
                        x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب));

                int finalRotations =
                    await _context.TrainingRotations
                        .CountAsync();

                var finalNumbers =
                    await _context.Doctors
                        .AsNoTracking()
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب))
                        .Select(x =>
                            x.رقم_الطبيب!)
                        .ToListAsync();

                var finalDuplicateNumbers =
                    finalNumbers
                        .Select(x => x.Trim())
                        .GroupBy(
                            x => x,
                            StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                bool doctorsMatch =
                    finalDoctorsWithNumber ==
                    cleanDoctors.Count
                    &&
                    finalDuplicateNumbers.Count == 0;

                // =================================================
                // Commit
                // =================================================

                await transaction.CommitAsync();

                // =================================================
                // النتيجة
                // =================================================

                return Ok(new
                {
                    success = true,

                    message =
                        doctorsMatch
                            ? "تمت المزامنة الكاملة بنجاح وأصبحت بيانات HospitalWeb مطابقة لبيانات Access."
                            : "تمت المزامنة، ولكن يوجد اختلاف يحتاج إلى فحص.",

                    accessRecords =
                        doctors.Count,

                    validAccessRecords =
                        validDoctors.Count,

                    uniqueAccessDoctors =
                        cleanDoctors.Count,

                    finalDoctors =
                        finalDoctors,

                    finalDoctorsWithNumber =
                        finalDoctorsWithNumber,

                    finalRotations =
                        finalRotations,

                    added =
                        added,

                    updated =
                        updated,

                    deleted =
                        deleted,

                    duplicateRemoved =
                        duplicateRemoved,

                    finalDuplicateNumbers =
                        finalDuplicateNumbers.Count,

                    rotationsAdded =
                        rotationsAdded,

                    rotationsUpdated =
                        rotationsUpdated,

                    rotationsDeleted =
                        rotationsDeleted,

                    doctorsMatch =
                        doctorsMatch
                });
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // تجاهل خطأ Rollback
                }

                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "حدث خطأ أثناء المزامنة، وتم إلغاء جميع التغييرات.",

                        error =
                            ex.Message,

                        type =
                            ex.GetType().FullName,

                        innerError =
                            ex.InnerException?.Message,

                        innerInnerError =
                            ex.InnerException?.InnerException?.Message
                    });
            }
        }

        // =========================================================
        // إنشاء الأقسام
        // =========================================================

        private async Task<List<Department>>
            EnsureDepartmentsAsync()
        {
            var requiredDepartments =
                new Dictionary<int, string>
                {
                    { 1, "الجراحة" },
                    { 2, "الباطني" },
                    { 3, "النسائية" },
                    { 4, "الأطفال" },
                    { 5, "الطوارئ" },
                    { 6, "الاختياري" }
                };

            var departments =
                await _context.Departments
                    .ToListAsync();

            bool changed = false;

            foreach (var item in requiredDepartments)
            {
                var department =
                    departments.FirstOrDefault(
                        x => x.Id == item.Key);

                if (department == null)
                {
                    department =
                        new Department
                        {
                            Id = item.Key,
                            Name = item.Value
                        };

                    _context.Departments.Add(
                        department);

                    departments.Add(
                        department);

                    changed = true;
                }
                else if (department.Name !=
                         item.Value)
                {
                    department.Name =
                        item.Value;

                    changed = true;
                }
            }

            if (changed)
            {
                PrepareDatesForPostgreSql();

                await SaveChangesSafeAsync(
                    "إنشاء الأقسام");
            }

            return departments
                .Where(x =>
                    requiredDepartments.ContainsKey(
                        x.Id))
                .OrderBy(x => x.Id)
                .ToList();
        }

        // =========================================================
        // مزامنة دورة تدريب
        // =========================================================

        private void SyncRotation(
            Doctor doctor,
            string departmentName,
            DateTime? startDate,
            DateTime? endDate,
            List<Department> departments,
            ref int rotationsAdded,
            ref int rotationsUpdated,
            ref int rotationsDeleted)
        {
            var department =
                departments.FirstOrDefault(
                    x => x.Name == departmentName);

            if (department == null)
            {
                return;
            }

            var rotations =
                doctor.TrainingRotations
                    .Where(x =>
                        x.DepartmentId ==
                        department.Id)
                    .ToList();

            // =====================================================
            // Access لا يحتوي على هذه الدورة
            // =====================================================

            if (!startDate.HasValue ||
                !endDate.HasValue)
            {
                foreach (var rotation in rotations)
                {
                    _context.TrainingRotations
                        .Remove(rotation);

                    rotationsDeleted++;
                }

                return;
            }

            DateTime newStartDate =
                ToUtcDate(startDate)!.Value;

            DateTime newEndDate =
                ToUtcDate(endDate)!.Value;

            // =====================================================
            // إضافة
            // =====================================================

            TrainingRotation? currentRotation =
                rotations.FirstOrDefault();

            if (currentRotation == null)
            {
                currentRotation =
                    new TrainingRotation
                    {
                        DoctorId =
                            doctor.Id,

                        DepartmentId =
                            department.Id,

                        StartDate =
                            newStartDate,

                        EndDate =
                            newEndDate
                    };

                _context.TrainingRotations
                    .Add(currentRotation);

                rotationsAdded++;
            }

            // =====================================================
            // تحديث
            // =====================================================

            else
            {
                bool changed = false;

                if (currentRotation.StartDate !=
                    newStartDate)
                {
                    currentRotation.StartDate =
                        newStartDate;

                    changed = true;
                }

                if (currentRotation.EndDate !=
                    newEndDate)
                {
                    currentRotation.EndDate =
                        newEndDate;

                    changed = true;
                }

                if (changed)
                {
                    rotationsUpdated++;
                }

                // =================================================
                // حذف تكرارات نفس الطبيب والقسم
                // =================================================

                foreach (var duplicate in
                         rotations.Skip(1))
                {
                    _context.TrainingRotations
                        .Remove(duplicate);

                    rotationsDeleted++;
                }
            }
        }

        // =========================================================
        // تحويل التاريخ إلى UTC
        // =========================================================

        private static DateTime? ToUtcDate(
            DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return DateTime.SpecifyKind(
                value.Value.Date,
                DateTimeKind.Utc);
        }

        // =========================================================
        // تجهيز جميع DateTime قبل PostgreSQL
        // =========================================================

        private void PrepareDatesForPostgreSql()
        {
            foreach (var entry in
                     _context.ChangeTracker.Entries())
            {
                foreach (var property in
                         entry.Properties)
                {
                    // CurrentValue
                    if (property.CurrentValue
                        is DateTime currentDate)
                    {
                        property.CurrentValue =
                            DateTime.SpecifyKind(
                                currentDate,
                                DateTimeKind.Utc);
                    }

                    // OriginalValue
                    if (property.OriginalValue
                        is DateTime originalDate)
                    {
                        property.OriginalValue =
                            DateTime.SpecifyKind(
                                originalDate,
                                DateTimeKind.Utc);
                    }
                }
            }
        }

        // =========================================================
        // حفظ آمن مع تشخيص DateTime
        // =========================================================

        private async Task SaveChangesSafeAsync(
            string operation)
        {
            try
            {
                PrepareDatesForPostgreSql();

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                var details =
                    GetDateTimeDiagnostics();

                throw new Exception(
                    "فشل حفظ البيانات أثناء العملية: "
                    + operation
                    + Environment.NewLine
                    + Environment.NewLine
                    + details,
                    ex);
            }
        }

        // =========================================================
        // تشخيص قيم DateTime
        // =========================================================

        private string GetDateTimeDiagnostics()
        {
            var result =
                new System.Text.StringBuilder();

            result.AppendLine(
                "تشخيص قيم التاريخ:");

            foreach (var entry in
                     _context.ChangeTracker.Entries())
            {
                foreach (var property in
                         entry.Properties)
                {
                    if (property.CurrentValue
                        is DateTime dateTime)
                    {
                        result.AppendLine(
                            "Entity = "
                            + entry.Entity.GetType().Name
                            + " | Property = "
                            + property.Metadata.Name
                            + " | Value = "
                            + dateTime.ToString("O")
                            + " | Kind = "
                            + dateTime.Kind);
                    }

                    if (property.OriginalValue
                        is DateTime originalDateTime)
                    {
                        result.AppendLine(
                            "Original Entity = "
                            + entry.Entity.GetType().Name
                            + " | Property = "
                            + property.Metadata.Name
                            + " | Value = "
                            + originalDateTime.ToString("O")
                            + " | Kind = "
                            + originalDateTime.Kind);
                    }
                }
            }

            return result.ToString();
        }

        // =========================================================
        // نموذج المزامنة
        // =========================================================

        public class DoctorSyncModel
        {
            public string? الرقم { get; set; }

            public string? الاسم { get; set; }

            public string? مكان_المباشرة { get; set; }

            public DateTime? تاريخ_المباشرة { get; set; }

            public DateTime? الجراحة_مباشرة { get; set; }

            public DateTime? الجراحة_انتهاء { get; set; }

            public DateTime? الباطني_مباشرة { get; set; }

            public DateTime? الباطني_انتهاء { get; set; }

            public DateTime? النسائية_مباشرة { get; set; }

            public DateTime? النسائية_انتهاء { get; set; }

            public DateTime? الاطفال_مباشرة { get; set; }

            public DateTime? الاطفال_انتهاء { get; set; }

            public DateTime? الطوارئ_مباشرة { get; set; }

            public DateTime? الطوارئ_انتهاء { get; set; }

            public DateTime? الاختياري_مباشرة { get; set; }

            public DateTime? الاختياري_انتهاء { get; set; }

            public string? ImagePath { get; set; }

            public string? Phone { get; set; }
        }
    }
}
