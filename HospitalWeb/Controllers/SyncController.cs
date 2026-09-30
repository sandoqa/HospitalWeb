
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

        public SyncController(
            ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // اختبار الاتصال
        // =========================================================

        [HttpGet("Test")]
        public IActionResult Test()
        {
            return Ok("Hospital Sync API يعمل بنجاح");
        }


        // =========================================================
        // فحص الأطباء الموجودين في HospitalWeb
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

                int total = doctors.Count;

                int withNumber = doctors.Count(x =>
                    !string.IsNullOrWhiteSpace(x.رقم_الطبيب));

                int withoutNumber = doctors.Count(x =>
                    string.IsNullOrWhiteSpace(x.رقم_الطبيب));

                // يتم تنفيذ GroupBy هنا في الذاكرة
                // لأن doctors تم تحميلها بواسطة ToListAsync()
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
                        Names = g
                            .Select(x => x.الاسم)
                            .ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,

                    totalDoctors = total,

                    doctorsWithNumber = withNumber,

                    doctorsWithoutNumber = withoutNumber,

                    duplicateNumbersCount =
                        duplicateNumbers.Count,

                    duplicateNumbers =
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
                        innerError =
                            ex.InnerException?.Message,
                        type =
                            ex.GetType().FullName
                    });
            }
        }


        // =========================================================
        // استقبال المزامنة الكاملة
        //
        // المصدر الرئيسي:
        // Visual Basic / Access
        //
        // HospitalWeb = نسخة من بيانات Access
        // =========================================================

        [HttpPost("Receive")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Receive(
            [FromBody] List<DoctorSyncModel> doctors)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // =================================================
                // 1. التحقق من البيانات
                // =================================================

                if (doctors == null || doctors.Count == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "لا توجد بيانات للمزامنة."
                    });
                }


                // =================================================
                // العدادات
                // =================================================

                int added = 0;
                int updated = 0;
                int deleted = 0;

                int duplicateRemoved = 0;

                int rotationsAdded = 0;
                int rotationsUpdated = 0;
                int rotationsDeleted = 0;


                // =================================================
                // 2. تنظيف بيانات Access
                // =================================================

                var validDoctors =
                    doctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.الرقم))
                        .ToList();


                // =================================================
                // 3. إزالة التكرار من Access
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
                // 4. إنشاء مجموعة أرقام Access
                // =================================================

                var accessDoctorNumbers =
                    cleanDoctors
                        .Select(x => x.الرقم!.Trim())
                        .ToHashSet(
                            StringComparer.OrdinalIgnoreCase);


                // =================================================
                // 5. التأكد من وجود الأقسام الستة
                // =================================================

                var departments =
                    await EnsureDepartmentsAsync();


                // =================================================
                // 6. قراءة جميع الأطباء الموجودين في HospitalWeb
                // =================================================

                var existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
                        .ToListAsync();


                // =================================================
                // 7. تنظيف التكرارات الموجودة في HospitalWeb
                // =================================================

                // GroupBy يتم هنا في الذاكرة لأن
                // existingDoctors تم تحميلها بواسطة ToListAsync()

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


                foreach (var duplicateDoctor
                    in duplicateDoctors)
                {
                    if (duplicateDoctor.TrainingRotations != null &&
                        duplicateDoctor.TrainingRotations.Count > 0)
                    {
                        foreach (var rotation
                            in duplicateDoctor.TrainingRotations.ToList())
                        {
                            _context.TrainingRotations.Remove(
                                rotation);

                            rotationsDeleted++;
                        }
                    }

                    _context.Doctors.Remove(
                        duplicateDoctor);

                    duplicateRemoved++;
                    deleted++;
                }


                if (duplicateDoctors.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }


                // =================================================
                // 8. إعادة قراءة الأطباء بعد تنظيف التكرارات
                // =================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
                        .ToListAsync();


                // =================================================
                // 9. حذف الأطباء غير الموجودين في Access
                // =================================================

                var doctorsToDelete =
                    existingDoctors
                        .Where(x =>
                            string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب)
                            ||
                            !accessDoctorNumbers.Contains(
                                x.رقم_الطبيب.Trim()))
                        .ToList();


                foreach (var doctor
                    in doctorsToDelete)
                {
                    if (doctor.TrainingRotations != null &&
                        doctor.TrainingRotations.Count > 0)
                    {
                        foreach (var rotation
                            in doctor.TrainingRotations.ToList())
                        {
                            _context.TrainingRotations.Remove(
                                rotation);

                            rotationsDeleted++;
                        }
                    }

                    _context.Doctors.Remove(doctor);

                    deleted++;
                }


                if (doctorsToDelete.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }


                // =================================================
                // 10. إعادة قراءة الأطباء بعد الحذف
                // =================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
                        .ToListAsync();


                // =================================================
                // 11. Dictionary للبحث السريع
                // =================================================

                // GroupBy هنا في الذاكرة

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
                // 12. معالجة أطباء Access
                // =================================================

                foreach (var source in cleanDoctors)
                {
                    string doctorNumber =
                        source.الرقم!.Trim();

                    Doctor? doctor = null;

                    doctorDictionary.TryGetValue(
                        doctorNumber,
                        out doctor);


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
                                    source.تاريخ_المباشرة,

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
                            source.تاريخ_المباشرة)
                        {
                            doctor.تاريخ_المباشرة =
                                source.تاريخ_المباشرة;

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
                // 13. حفظ الأطباء
                // =================================================

                await _context.SaveChangesAsync();


                // =================================================
                // 14. إعادة تحميل الأطباء والتدريبات
                // =================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
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
                // 15. مزامنة تدريبات جميع الأطباء
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

                    SyncRotation(
                        doctor,
                        "الجراحة",
                        source.الجراحة_مباشرة,
                        source.الجراحة_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    SyncRotation(
                        doctor,
                        "الباطني",
                        source.الباطني_مباشرة,
                        source.الباطني_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    SyncRotation(
                        doctor,
                        "النسائية",
                        source.النسائية_مباشرة,
                        source.النسائية_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    SyncRotation(
                        doctor,
                        "الأطفال",
                        source.الاطفال_مباشرة,
                        source.الاطفال_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

                    SyncRotation(
                        doctor,
                        "الطوارئ",
                        source.الطوارئ_مباشرة,
                        source.الطوارئ_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);

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
                // 16. حفظ التدريبات
                // =================================================

                await _context.SaveChangesAsync();


                // =================================================
                // 17. تنظيف نهائي للتدريبات
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


                foreach (var rotation
                    in duplicateRotations)
                {
                    _context.TrainingRotations.Remove(
                        rotation);

                    duplicateRemoved++;
                    rotationsDeleted++;
                }


                if (duplicateRotations.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }


                // =================================================
                // 18. قراءة النتائج النهائية
                // =================================================

                int finalDoctorsCount =
                    await _context.Doctors.CountAsync();

                int finalDoctorsWithNumber =
                    await _context.Doctors
                        .CountAsync(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب));

                int finalRotationsCount =
                    await _context.TrainingRotations.CountAsync();


                // =================================================
                // 19. فحص التكرارات النهائية
                //
                // مهم:
                // لا نستخدم GroupBy + StringComparer داخل SQLite.
                // نقوم أولاً بتحميل الأرقام إلى الذاكرة.
                // =================================================

                var finalDoctorNumbers =
                    await _context.Doctors
                        .AsNoTracking()
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب))
                        .Select(x =>
                            x.رقم_الطبيب!)
                        .ToListAsync();


                var finalDuplicateNumbers =
                    finalDoctorNumbers
                        .Select(x => x.Trim())
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x))
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
                // 20. تأكيد العملية بالكامل
                // =================================================

                await transaction.CommitAsync();


                // =================================================
                // 21. النتيجة النهائية
                // =================================================

                return Ok(new
                {
                    success = true,

                    message =
                        doctorsMatch
                            ? "تمت المزامنة الكاملة بنجاح وأصبح عدد الأطباء في HospitalWeb مطابقًا لبيانات Access."
                            : "تمت المزامنة، لكن يوجد اختلاف يحتاج إلى فحص.",

                    accessRecords =
                        doctors.Count,

                    validAccessRecords =
                        validDoctors.Count,

                    uniqueAccessDoctors =
                        cleanDoctors.Count,

                    finalDoctors =
                        finalDoctorsCount,

                    finalDoctorsWithNumber =
                        finalDoctorsWithNumber,

                    finalRotations =
                        finalRotationsCount,

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
                            ex.InnerException?.Message
                    });
            }
        }


        // =========================================================
        // التأكد من وجود الأقسام
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


            foreach (var item
                in requiredDepartments)
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
                else if (department.Name != item.Value)
                {
                    department.Name =
                        item.Value;

                    changed = true;
                }
            }


            if (changed)
            {
                await _context.SaveChangesAsync();
            }


            return departments
                .Where(x =>
                    requiredDepartments.ContainsKey(x.Id))
                .OrderBy(x => x.Id)
                .ToList();
        }


        // =========================================================
        // مزامنة تدريب واحد
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
                    x =>
                        x.Name == departmentName);


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


            if (!startDate.HasValue ||
                !endDate.HasValue)
            {
                foreach (var rotation
                    in rotations)
                {
                    _context.TrainingRotations.Remove(
                        rotation);

                    rotationsDeleted++;
                }

                return;
            }


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
                            startDate.Value,

                        EndDate =
                            endDate.Value
                    };

                _context.TrainingRotations.Add(
                    currentRotation);

                rotationsAdded++;
            }
            else
            {
                bool changed = false;


                if (currentRotation.StartDate !=
                    startDate.Value)
                {
                    currentRotation.StartDate =
                        startDate.Value;

                    changed = true;
                }


                if (currentRotation.EndDate !=
                    endDate.Value)
                {
                    currentRotation.EndDate =
                        endDate.Value;

                    changed = true;
                }


                if (changed)
                {
                    rotationsUpdated++;
                }


                foreach (var duplicate
                    in rotations.Skip(1))
                {
                    _context.TrainingRotations.Remove(
                        duplicate);

                    rotationsDeleted++;
                }
            }
        }


        // =========================================================
        // Model استقبال البيانات من Visual Basic
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
