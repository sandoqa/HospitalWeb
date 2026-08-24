
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalWeb.Data;
using HospitalWeb.Models;

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
        // اختبار الاتصال
        // =========================================================

        [HttpGet("Test")]
        public IActionResult Test()
        {
            return Ok("Hospital Sync API يعمل بنجاح");
        }

        
// =========================================================
// فحص الأطباء الموجودين في الموقع
// =========================================================

[HttpGet("CheckDoctors")]
public async Task<IActionResult> CheckDoctors()
        {
            try
            {
                var doctors = await _context.Doctors
                    .AsNoTracking()
                    .ToListAsync();

                int total = doctors.Count;

                int withNumber = doctors.Count(x =>
                    !string.IsNullOrWhiteSpace(x.رقم_الطبيب));

                int withoutNumber = doctors.Count(x =>
                    string.IsNullOrWhiteSpace(x.رقم_الطبيب));

                var duplicateNumbers = doctors
                    .Where(x => !string.IsNullOrWhiteSpace(x.رقم_الطبيب))
                    .GroupBy(x => x.رقم_الطبيب!.Trim())
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

                    totalDoctors = total,

                    doctorsWithNumber = withNumber,

                    doctorsWithoutNumber = withoutNumber,

                    duplicateNumbersCount = duplicateNumbers.Count,

                    duplicateNumbers = duplicateNumbers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }


        // =========================================================
        // استقبال المزامنة
        // =========================================================

        [HttpPost("Receive")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Receive(
            [FromBody] List<DoctorSyncModel> doctors)
        {
            try
            {
                if (doctors == null || doctors.Count == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "لا توجد بيانات للمزامنة."
                    });
                }


                int added = 0;
                int updated = 0;
                int deleted = 0;

                int duplicateRemoved = 0;

                int rotationsAdded = 0;
                int rotationsUpdated = 0;
                int rotationsDeleted = 0;


                // =====================================================
                // 1. تنظيف بيانات Access
                // =====================================================

                var validDoctors =
                    doctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x.الرقم))
                        .ToList();


                // =====================================================
                // 2. منع التكرار في Access
                // =====================================================

                var cleanDoctors =
                    validDoctors
                        .GroupBy(x =>
                            x.الرقم!.Trim(),
                            StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();


                duplicateRemoved +=
                    validDoctors.Count - cleanDoctors.Count;


                // =====================================================
                // 3. الأقسام
                // =====================================================

                string[] departmentNames =
                {
                    "الجراحة",
                    "الباطني",
                    "النسائية",
                    "الأطفال",
                    "الطوارئ",
                    "الاختياري"
                };


                var departments =
                    await _context.Departments.ToListAsync();


                foreach (var departmentName in departmentNames)
                {
                    if (!departments.Any(
                        x => x.Name == departmentName))
                    {
                        _context.Departments.Add(
                            new Department
                            {
                                Name = departmentName
                            });
                    }
                }


                await _context.SaveChangesAsync();


                departments =
                    await _context.Departments.ToListAsync();


                // =====================================================
                // 4. أرقام الأطباء الموجودة في Access
                // =====================================================

                var accessDoctorNumbers =
                    cleanDoctors
                        .Select(x => x.الرقم!.Trim())
                        .ToHashSet(
                            StringComparer.OrdinalIgnoreCase);


                // =====================================================
                // 5. قراءة جميع الأطباء الموجودين في HospitalWeb
                // =====================================================

                var existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
                        .ToListAsync();


                // =====================================================
                // 6. تنظيف التكرارات الموجودة في HospitalWeb
                //
                // إذا كان نفس رقم الطبيب موجودًا أكثر من مرة
                // نحتفظ بسجل واحد فقط.
                // =====================================================

                var duplicateDoctors =
                    existingDoctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.رقم_الطبيب))
                        .GroupBy(x =>
                            x.رقم_الطبيب!.Trim(),
                            StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g =>
                            g.Skip(1))
                        .ToList();


                foreach (var duplicateDoctor
                    in duplicateDoctors)
                {
                    _context.Doctors.Remove(
                        duplicateDoctor);

                    duplicateRemoved++;
                    deleted++;
                }


                await _context.SaveChangesAsync();


                // =====================================================
                // إعادة قراءة الأطباء بعد تنظيف التكرار
                // =====================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
                        .ToListAsync();


                // =====================================================
                // 7. حذف جميع الأطباء غير الموجودين في Access
                //
                // يشمل:
                // - الطبيب الذي رقمه غير موجود في Access
                // - الطبيب الذي ليس لديه رقم طبيب
                // =====================================================

                var doctorsToDelete =
    existingDoctors
        .Where(x =>
            string.IsNullOrWhiteSpace(x.رقم_الطبيب) ||
            !accessDoctorNumbers.Contains(
                x.رقم_الطبيب.Trim()))
        .ToList();


                foreach (var doctor
                    in doctorsToDelete)
                {
                    _context.Doctors.Remove(doctor);

                    deleted++;
                }


                await _context.SaveChangesAsync();


                // =====================================================
                // إعادة قراءة الأطباء بعد الحذف
                // =====================================================

                existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
                        .ToListAsync();


                // =====================================================
                // 8. معالجة أطباء Access
                // =====================================================

                foreach (var source in cleanDoctors)
                {
                    string doctorNumber =
                        source.الرقم!.Trim();


                    // =================================================
                    // البحث بواسطة رقم الطبيب
                    // =================================================

                    var doctor =
                        existingDoctors.FirstOrDefault(
                            x =>
                                !string.IsNullOrWhiteSpace(
                                    x.رقم_الطبيب)
                                &&
                                x.رقم_الطبيب.Trim()
                                    .Equals(
                                        doctorNumber,
                                        StringComparison.OrdinalIgnoreCase));


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

                        await _context.SaveChangesAsync();

                        existingDoctors.Add(doctor);

                        added++;
                    }
                    else
                    {
                        // =============================================
                        // تحديث بيانات الطبيب
                        // =============================================

                        bool changed = false;


                        if (doctor.الاسم !=
                            (source.الاسم ?? ""))
                        {
                            doctor.الاسم =
                                source.الاسم ?? "";

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


                    // =================================================
                    // مزامنة الجراحة
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
                    // مزامنة الباطني
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
                    // مزامنة النسائية
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
                    // مزامنة الأطفال
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
                    // مزامنة الطوارئ
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
                    // مزامنة الاختياري
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


                // =====================================================
                // حفظ تحديثات الأطباء والتدريبات
                // =====================================================

                await _context.SaveChangesAsync();


                // =====================================================
                // 9. تنظيف نهائي للتدريبات المكررة
                //
                // نفس الطبيب + نفس القسم = سجل واحد فقط
                // =====================================================

                var allRotations =
                    await _context.TrainingRotations
                        .ToListAsync();


                var duplicateRotations =
                    allRotations
                        .GroupBy(x =>
                            new
                            {
                                x.DoctorId,
                                x.DepartmentId
                            })
                        .Where(g => g.Count() > 1)
                        .SelectMany(g =>
                            g.Skip(1))
                        .ToList();


                foreach (var rotation
                    in duplicateRotations)
                {
                    _context.TrainingRotations.Remove(
                        rotation);

                    duplicateRemoved++;
                    rotationsDeleted++;
                }


                await _context.SaveChangesAsync();


                // =====================================================
                // 10. التحقق النهائي من عدد الأطباء
                // =====================================================

                int finalDoctorsCount =
                    await _context.Doctors.CountAsync();


                int finalRotationsCount =
                    await _context.TrainingRotations.CountAsync();


                // =====================================================
                // النتيجة
                // =====================================================

                return Ok(new
                {
                    success = true,

                    message =
                        "تمت المزامنة الكاملة والتنظيف بنجاح",

                    added =
                        added,

                    updated =
                        updated,

                    deleted =
                        deleted,

                    duplicateRemoved =
                        duplicateRemoved,

                    rotationsAdded =
                        rotationsAdded,

                    rotationsUpdated =
                        rotationsUpdated,

                    rotationsDeleted =
                        rotationsDeleted,

                    accessRecords =
                        doctors.Count,

                    validAccessRecords =
                        validDoctors.Count,

                    uniqueAccessDoctors =
                        cleanDoctors.Count,

                    finalDoctors =
                        finalDoctorsCount,

                    finalRotations =
                        finalRotationsCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "حدث خطأ أثناء المزامنة",

                        error =
                            ex.Message,

                        innerError =
                            ex.InnerException?.Message
                    });
            }
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
                return;


            var rotations =
                doctor.TrainingRotations
                    .Where(x =>
                        x.DepartmentId ==
                        department.Id)
                    .ToList();


            // =====================================================
            // لا توجد تواريخ في Access
            // نحذف تدريب هذا القسم
            // =====================================================

            if (!startDate.HasValue ||
                !endDate.HasValue)
            {
                foreach (var rotation
                    in rotations)
                {
                    _context.TrainingRotations
                        .Remove(rotation);

                    rotationsDeleted++;
                }

                return;
            }


            // =====================================================
            // لا يوجد تدريب سابق
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
                            startDate.Value,

                        EndDate =
                            endDate.Value
                    };


                _context.TrainingRotations
                    .Add(currentRotation);

                rotationsAdded++;
            }
            else
            {
                // =================================================
                // تحديث التواريخ
                // =================================================

                if (currentRotation.StartDate !=
                    startDate.Value
                    ||
                    currentRotation.EndDate !=
                    endDate.Value)
                {
                    currentRotation.StartDate =
                        startDate.Value;

                    currentRotation.EndDate =
                        endDate.Value;

                    rotationsUpdated++;
                }


                // =================================================
                // حذف التدريبات المكررة
                // =================================================

                foreach (var duplicate
                    in rotations.Skip(1))
                {
                    _context.TrainingRotations
                        .Remove(duplicate);

                    rotationsDeleted++;
                }
            }
        }


        // =========================================================
        // Model استقبال البيانات من VB.NET
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

