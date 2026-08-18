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
        // استقبال المزامنة من برنامج VB.NET
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


                // =====================================================
                // الأقسام
                // =====================================================

                var departments = await _context.Departments
                    .ToListAsync();


                // =====================================================
                // التأكد من وجود الأقسام
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


                // إعادة تحميل الأقسام
                departments = await _context.Departments
                    .ToListAsync();


                // =====================================================
                // أرقام الأطباء الموجودة في Access
                // =====================================================

                var accessDoctorNumbers =
                    doctors
                        .Where(x => !string.IsNullOrWhiteSpace(x.الرقم))
                        .Select(x => x.الرقم!.Trim())
                        .ToHashSet();


                // =====================================================
                // قراءة الأطباء الموجودين في الموقع
                // =====================================================

                var existingDoctors =
                    await _context.Doctors
                        .Include(x => x.TrainingRotations)
                        .ToListAsync();


                // =====================================================
                // إضافة / تعديل
                // =====================================================

                foreach (var source in doctors)
                {
                    if (string.IsNullOrWhiteSpace(source.الرقم))
                        continue;


                    string doctorNumber =
                        source.الرقم.Trim();


                    var doctor =
                        existingDoctors.FirstOrDefault(
                            x => x.رقم_الطبيب == doctorNumber);


                    // =================================================
                    // طبيب جديد
                    // =================================================

                    if (doctor == null)
                    {
                        doctor = new Doctor
                        {
                            رقم_الطبيب = doctorNumber,
                            الاسم = source.الاسم ?? "",
                            مكان_المباشرة = source.مكان_المباشرة,
                            تاريخ_المباشرة = source.تاريخ_المباشرة,
                            Phone = source.Phone,
                            ImagePath = source.ImagePath
                        };


                        _context.Doctors.Add(doctor);

                        await _context.SaveChangesAsync();

                        added++;

                        existingDoctors.Add(doctor);
                    }
                    else
                    {
                        // =================================================
                        // تعديل بيانات الطبيب
                        // =================================================

                        bool changed = false;


                        if (doctor.الاسم != source.الاسم)
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


                        if (doctor.Phone != source.Phone)
                        {
                            doctor.Phone =
                                source.Phone;

                            changed = true;
                        }


                        if (doctor.ImagePath != source.ImagePath)
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
                    // مزامنة الأقسام
                    // =================================================

                    await SyncRotation(
                        doctor,
                        "الجراحة",
                        source.الجراحة_مباشرة,
                        source.الجراحة_انتهاء,
                        departments);


                    await SyncRotation(
                        doctor,
                        "الباطني",
                        source.الباطني_مباشرة,
                        source.الباطني_انتهاء,
                        departments);


                    await SyncRotation(
                        doctor,
                        "النسائية",
                        source.النسائية_مباشرة,
                        source.النسائية_انتهاء,
                        departments);


                    await SyncRotation(
                        doctor,
                        "الأطفال",
                        source.الاطفال_مباشرة,
                        source.الاطفال_انتهاء,
                        departments);


                    await SyncRotation(
                        doctor,
                        "الطوارئ",
                        source.الطوارئ_مباشرة,
                        source.الطوارئ_انتهاء,
                        departments);


                    await SyncRotation(
                        doctor,
                        "الاختياري",
                        source.الاختياري_مباشرة,
                        source.الاختياري_انتهاء,
                        departments);
                }


                await _context.SaveChangesAsync();


                // =====================================================
                // حذف الأطباء الذين لم يعودوا موجودين في Access
                // =====================================================

                var doctorsToDelete =
                    existingDoctors
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x.رقم_الطبيب) &&
                            !accessDoctorNumbers.Contains(
                                x.رقم_الطبيب.Trim()))
                        .ToList();


                foreach (var doctor in doctorsToDelete)
                {
                    _context.Doctors.Remove(doctor);

                    deleted++;
                }


                await _context.SaveChangesAsync();


                // =====================================================
                // النتيجة
                // =====================================================

                return Ok(new
                {
                    success = true,
                    message = "تمت المزامنة بنجاح",
                    added = added,
                    updated = updated,
                    deleted = deleted,
                    total = doctors.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message = "حدث خطأ أثناء المزامنة",
                        error = ex.Message,
                        innerError = ex.InnerException?.Message
                    });
            }
        }


        // =========================================================
        // مزامنة تدريب واحد
        // =========================================================

        private async Task SyncRotation(
            Doctor doctor,
            string departmentName,
            DateTime? startDate,
            DateTime? endDate,
            List<Department> departments)
        {
            var department =
                departments.FirstOrDefault(
                    x => x.Name == departmentName);


            if (department == null)
                return;


            var rotation =
                doctor.TrainingRotations
                    .FirstOrDefault(
                        x => x.DepartmentId ==
                             department.Id);


            // =====================================================
            // لا توجد تواريخ في Access
            // =====================================================

            if (!startDate.HasValue ||
                !endDate.HasValue)
            {
                if (rotation != null)
                {
                    _context.TrainingRotations.Remove(rotation);
                }

                return;
            }


            // =====================================================
            // تدريب جديد
            // =====================================================

            if (rotation == null)
            {
                rotation = new TrainingRotation
                {
                    DoctorId = doctor.Id,
                    DepartmentId = department.Id,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value
                };

                _context.TrainingRotations.Add(rotation);
            }
            else
            {
                // =================================================
                // تعديل التدريب
                // =================================================

                rotation.StartDate =
                    startDate.Value;

                rotation.EndDate =
                    endDate.Value;
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