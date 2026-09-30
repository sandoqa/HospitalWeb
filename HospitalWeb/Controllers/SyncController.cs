
using HospitalWeb.Controllers;
using HospitalWeb.Data;
using HospitalWeb.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HospitalWeb.Controllers
{
    [ApiController]
    [Route("Sync")]
    public class SyncController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SyncController> _logger;

        public SyncController(
            ApplicationDbContext context,
            ILogger<SyncController> logger)
        {
            _context = context;
            _logger = logger;
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
                _logger.LogError(
                    ex,
                    "❌ خطأ أثناء CheckDoctors");

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
                //
                // نستبعد السجلات التي لا تحتوي رقم طبيب.
                // =================================================

                var validDoctors =
                    doctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.الرقم))
                        .ToList();


                // =================================================
                // 3. إزالة التكرار من Access
                //
                // الرقم هو المفتاح الفريد للطبيب.
                //
                // إذا تكرر الرقم:
                // نحتفظ بأول سجل فقط.
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
                //
                // هذه المجموعة هي المرجع الأساسي للحذف.
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
                //
                // إذا كان رقم الطبيب مكررًا:
                // نحتفظ بأول طبيب ونحذف الباقي.
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


                foreach (var duplicateDoctor
                    in duplicateDoctors)
                {
                    // ---------------------------------------------
                    // حذف تدريبات الطبيب المكرر أولًا
                    // ---------------------------------------------

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


                    // ---------------------------------------------
                    // حذف الطبيب المكرر
                    // ---------------------------------------------

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
                //
                // أي طبيب في Web لا يوجد رقمه في Access
                // سيتم حذفه.
                //
                // وكذلك أي طبيب بدون رقم.
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
                    // ---------------------------------------------
                    // حذف تدريبات الطبيب أولًا
                    // ---------------------------------------------

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


                    // ---------------------------------------------
                    // حذف الطبيب
                    // ---------------------------------------------

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
                //
                // المفتاح = رقم الطبيب
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
                // 12. معالجة أطباء Access
                // =================================================

                foreach (var source in cleanDoctors)
                {
                    string doctorNumber =
                        source.الرقم!.Trim();


                    Doctor? doctor = null;


                    // =================================================
                    // البحث بواسطة رقم الطبيب
                    // =================================================

                    doctorDictionary.TryGetValue(
                        doctorNumber,
                        out doctor);


                    // =================================================
                    // طبيب جديد
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
                        // =============================================
                        // تحديث بيانات الطبيب
                        // =============================================

                        bool changed = false;


                        // ---------------------------------------------
                        // الاسم
                        // ---------------------------------------------

                        string newName =
                            source.الاسم ?? "";


                        if (doctor.الاسم != newName)
                        {
                            doctor.الاسم =
                                newName;

                            changed = true;
                        }


                        // ---------------------------------------------
                        // مكان المباشرة
                        // ---------------------------------------------

                        if (doctor.مكان_المباشرة !=
                            source.مكان_المباشرة)
                        {
                            doctor.مكان_المباشرة =
                                source.مكان_المباشرة;

                            changed = true;
                        }


                        // ---------------------------------------------
                        // تاريخ المباشرة
                        // ---------------------------------------------

                        if (doctor.تاريخ_المباشرة !=
                            source.تاريخ_المباشرة)
                        {
                            doctor.تاريخ_المباشرة =
                                source.تاريخ_المباشرة;

                            changed = true;
                        }


                        // ---------------------------------------------
                        // الهاتف
                        // ---------------------------------------------

                        if (doctor.Phone !=
                            source.Phone)
                        {
                            doctor.Phone =
                                source.Phone;

                            changed = true;
                        }


                        // ---------------------------------------------
                        // الصورة
                        // ---------------------------------------------

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
                //
                // مهم جدًا:
                // نحفظ قبل مزامنة التدريبات حتى يحصل الأطباء
                // الجدد على Id من قاعدة البيانات.
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


                    // =================================================
                    // الجراحة
                    // =================================================

                    SyncRotation(
                        doctor,
                        "الجراحة",
                        source.الجراحة_مباشرة,
                        source.الجراحة_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);


                    // =================================================
                    // الباطني
                    // =================================================

                    SyncRotation(
                        doctor,
                        "الباطني",
                        source.الباطني_مباشرة,
                        source.الباطني_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);


                    // =================================================
                    // النسائية
                    // =================================================

                    SyncRotation(
                        doctor,
                        "النسائية",
                        source.النسائية_مباشرة,
                        source.النسائية_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);


                    // =================================================
                    // الأطفال
                    // =================================================

                    SyncRotation(
                        doctor,
                        "الأطفال",
                        source.الاطفال_مباشرة,
                        source.الاطفال_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);


                    // =================================================
                    // الطوارئ
                    // =================================================

                    SyncRotation(
                        doctor,
                        "الطوارئ",
                        source.الطوارئ_مباشرة,
                        source.الطوارئ_انتهاء,
                        departments,
                        ref rotationsAdded,
                        ref rotationsUpdated,
                        ref rotationsDeleted);


                    // =================================================
                    // الاختياري
                    // =================================================

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
                //
                // نفس الطبيب + نفس القسم
                // يجب أن يكون له سجل واحد فقط.
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
                // =================================================

                var finalDuplicateNumbers =
                    await _context.Doctors
                        .AsNoTracking()
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب))
                        .GroupBy(
                            x => x.رقم_الطبيب!.Trim(),
                            StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToListAsync();


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
                // تسجيل نجاح المزامنة في Render
                // =================================================

                _logger.LogInformation(
                    "✅ SYNC SUCCESS - Access Records: {AccessRecords}, Unique Doctors: {UniqueDoctors}, Final Doctors: {FinalDoctors}, Added: {Added}, Updated: {Updated}, Deleted: {Deleted}, Rotations Added: {RotationsAdded}, Rotations Updated: {RotationsUpdated}, Rotations Deleted: {RotationsDeleted}",
                    doctors.Count,
                    cleanDoctors.Count,
                    finalDoctorsCount,
                    added,
                    updated,
                    deleted,
                    rotationsAdded,
                    rotationsUpdated,
                    rotationsDeleted);


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


                    // ---------------------------------------------
                    // Access
                    // ---------------------------------------------

                    accessRecords =
                        doctors.Count,

                    validAccessRecords =
                        validDoctors.Count,

                    uniqueAccessDoctors =
                        cleanDoctors.Count,


                    // ---------------------------------------------
                    // HospitalWeb
                    // ---------------------------------------------

                    finalDoctors =
                        finalDoctorsCount,

                    finalDoctorsWithNumber =
                        finalDoctorsWithNumber,

                    finalRotations =
                        finalRotationsCount,


                    // ---------------------------------------------
                    // الأطباء
                    // ---------------------------------------------

                    added =
                        added,

                    updated =
                        updated,

                    deleted =
                        deleted,


                    // ---------------------------------------------
                    // التكرارات
                    // ---------------------------------------------

                    duplicateRemoved =
                        duplicateRemoved,

                    finalDuplicateNumbers =
                        finalDuplicateNumbers.Count,


                    // ---------------------------------------------
                    // التدريبات
                    // ---------------------------------------------

                    rotationsAdded =
                        rotationsAdded,

                    rotationsUpdated =
                        rotationsUpdated,

                    rotationsDeleted =
                        rotationsDeleted,


                    // ---------------------------------------------
                    // التحقق النهائي
                    // ---------------------------------------------

                    doctorsMatch =
                        doctorsMatch
                });
            }
            catch (Exception ex)
            {
                // =================================================
                // في حالة الخطأ:
                // إلغاء كل تغييرات المزامنة
                // =================================================

                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(
                        rollbackEx,
                        "❌ فشل Rollback أثناء مزامنة HospitalWeb");
                }


                // =================================================
                // تسجيل الخطأ الحقيقي في Render
                // =================================================

                _logger.LogError(
                    ex,
                    "❌ SYNC ERROR - فشلت مزامنة بيانات الأطباء من Access");


                // =================================================
                // جمع جميع Inner Exceptions
                // =================================================

                var innerErrors =
                    new List<string>();


                Exception? currentException = ex;


                while (currentException != null)
                {
                    innerErrors.Add(
                        $"{currentException.GetType().FullName}: {currentException.Message}");

                    currentException =
                        currentException.InnerException;
                }


                // =================================================
                // إرجاع الخطأ إلى Visual Basic
                // =================================================

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

                        innerErrors =
                            innerErrors
                    });
            }
        }


        // =========================================================
        // التأكد من وجود الأقسام
        //
        // نستخدم IDs ثابتة للأقسام الستة.
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
                    // ---------------------------------------------
                    // الأقسام الستة الأساسية ثابتة
                    // ---------------------------------------------

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
            // =====================================================
            // البحث عن القسم
            // =====================================================

            var department =
                departments.FirstOrDefault(
                    x =>
                        x.Name == departmentName);


            if (department == null)
            {
                return;
            }


            // =====================================================
            // تدريبات الطبيب لهذا القسم
            // =====================================================

            var rotations =
                doctor.TrainingRotations
                    .Where(x =>
                        x.DepartmentId ==
                        department.Id)
                    .ToList();


            // =====================================================
            // لا توجد تواريخ في Access
            //
            // إذن يجب عدم وجود تدريب لهذا القسم في Web.
            // =====================================================

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


            // =====================================================
            // يوجد أكثر من تدريب لنفس القسم
            //
            // نحتفظ بأول سجل ونحذف الباقي.
            // =====================================================

            TrainingRotation? currentRotation =
                rotations.FirstOrDefault();


            if (currentRotation == null)
            {
                // =================================================
                // إنشاء تدريب جديد
                // =================================================

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
                // =================================================
                // تحديث تاريخ البداية
                // =================================================

                bool changed = false;


                if (currentRotation.StartDate !=
                    startDate.Value)
                {
                    currentRotation.StartDate =
                        startDate.Value;

                    changed = true;
                }


                // =================================================
                // تحديث تاريخ النهاية
                // =================================================

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


                // =================================================
                // حذف التدريبات المكررة
                // =================================================

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
            // -----------------------------------------------------
            // الرقم الأساسي للطبيب
            // -----------------------------------------------------

            public string? الرقم { get; set; }


            // -----------------------------------------------------
            // بيانات الطبيب
            // -----------------------------------------------------

            public string? الاسم { get; set; }

            public string? مكان_المباشرة { get; set; }

            public DateTime? تاريخ_المباشرة { get; set; }


            // -----------------------------------------------------
            // الجراحة
            // -----------------------------------------------------

            public DateTime? الجراحة_مباشرة { get; set; }

            public DateTime? الجراحة_انتهاء { get; set; }


            // -----------------------------------------------------
            // الباطني
            // -----------------------------------------------------

            public DateTime? الباطني_مباشرة { get; set; }

            public DateTime? الباطني_انتهاء { get; set; }


            // -----------------------------------------------------
            // النسائية
            // -----------------------------------------------------

            public DateTime? النسائية_مباشرة { get; set; }

            public DateTime? النسائية_انتهاء { get; set; }


            // -----------------------------------------------------
            // الأطفال
            // -----------------------------------------------------

            public DateTime? الاطفال_مباشرة { get; set; }

            public DateTime? الاطفال_انتهاء { get; set; }


            // -----------------------------------------------------
            // الطوارئ
            // -----------------------------------------------------

            public DateTime? الطوارئ_مباشرة { get; set; }

            public DateTime? الطوارئ_انتهاء { get; set; }


            // -----------------------------------------------------
            // الاختياري
            // -----------------------------------------------------

            public DateTime? الاختياري_مباشرة { get; set; }

            public DateTime? الاختياري_انتهاء { get; set; }


            // -----------------------------------------------------
            // الصورة
            // -----------------------------------------------------

            public string? ImagePath { get; set; }


            // -----------------------------------------------------
            // الهاتف
            // -----------------------------------------------------

            public string? Phone { get; set; }
        }
    }
}

