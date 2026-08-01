using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HospitalWeb.Data;
using HospitalWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;


namespace HospitalWeb.Controllers
{
    public class DoctorsController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;



        public DoctorsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }




        // =========================
        // قائمة الأطباء + البحث + التنبيه
        // =========================

        public async Task<IActionResult> Index(string search)
        {

            var doctors = _context.Doctors
                .Include(x => x.TrainingRotations)
                .ThenInclude(x => x.Department)
                .AsQueryable();



            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();


                doctors = doctors.Where(x =>
                    x.الاسم.StartsWith(search)
                    ||
                    (x.Phone != null &&
                     x.Phone.StartsWith(search))
                );
            }



            var result = await doctors
                .OrderBy(x => x.الاسم)
                .ToListAsync();



            Dictionary<int, int> warningDays = new();



            foreach (var doctor in result)
            {

                int days = GetWarningDays(doctor);

                Console.WriteLine(
    doctor.الاسم + " باقي " + days + " يوم"
);
                if (days >= 1 && days <= 5)
                {
                    warningDays[doctor.Id] = days;
                }

            }




            result = result
                .OrderBy(x =>
                    warningDays.ContainsKey(x.Id)
                    ? warningDays[x.Id]
                    : 999)
                .ThenBy(x => x.الاسم)
                .ToList();


            ViewBag.WarningDays = warningDays;

            ViewBag.Search = search;


            // =========================
            // إحصائيات الصفحة الرئيسية
            // =========================

            ViewBag.DoctorsCount =
                await _context.Doctors.CountAsync();


            ViewBag.DepartmentsCount =
                await _context.Departments.CountAsync();


            ViewBag.TrainingCount =
                await _context.TrainingRotations.CountAsync();

            // =========================
            // إحصائية الأطباء الموجودين حاليا في الأقسام
            // حسب تاريخ بداية ونهاية التدريب
            // =========================

            var today = DateTime.Today;


            var currentDepartments =
                await _context.TrainingRotations
                .Include(x => x.Department)
                .Where(x =>
                    x.StartDate.Date <= today &&
                    x.EndDate.Date >= today
                )
                .GroupBy(x => x.Department.Name)
                .Select(g => new
                {
                    Department = g.Key,

                    Count = g.Select(x => x.DoctorId)
                             .Distinct()
                             .Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();



            ViewBag.CurrentDepartments = currentDepartments;

            return View(result);

        }





        private int GetWarningDays(Doctor doctor)
        {
            if (doctor.TrainingRotations == null ||
                doctor.TrainingRotations.Count == 0)
            {
                return -1;
            }


            var currentRotation =
                doctor.TrainingRotations
                .Where(x =>
                    x.StartDate.Date <= DateTime.Today &&
                    x.EndDate.Date >= DateTime.Today)
                .OrderBy(x => x.EndDate)
                .FirstOrDefault();


            if (currentRotation == null)
                return -1;


            int remainingDays =
                (currentRotation.EndDate.Date -
                 DateTime.Today).Days;


            if (remainingDays >= 1 &&
                remainingDays <= 5)
            {
                return remainingDays;
            }


            return -1;
        }




        // =========================
        // ملف الطبيب
        // =========================

        public async Task<IActionResult> Details(int? id)
        {

            if (id == null)
                return NotFound();



            var doctor =
                await _context.Doctors
                .Include(x => x.TrainingRotations)
                .ThenInclude(x => x.Department)
                .FirstOrDefaultAsync(x => x.Id == id);



            if (doctor == null)
                return NotFound();



            return View(doctor);

        }
        // =========================
        // إضافة طبيب
        // =========================

        // =========================
        // إضافة طبيب
        // =========================

        public IActionResult Create()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Doctor doctor,
            IFormFile? imageFile)
        {

            if (!ModelState.IsValid)
            {
                return View(doctor);
            }


            try
            {

                // حفظ صورة الطبيب إذا وجدت
                if (imageFile != null && imageFile.Length > 0)
                {
                    doctor.ImagePath = await SaveImage(imageFile);
                }


                // إضافة الطبيب إلى قاعدة البيانات
                _context.Doctors.Add(doctor);

                await _context.SaveChangesAsync();


                Console.WriteLine(
                    "Doctor Added ID = " + doctor.Id
                );


                return RedirectToAction(nameof(Index));

            }
            catch (Exception ex)
            {

                Console.WriteLine(
                    "CREATE DOCTOR ERROR = " + ex.Message
                );


                ModelState.AddModelError(
                    "",
                    "حدث خطأ أثناء إضافة الطبيب: " + ex.Message
                );


                return View(doctor);
            }

        }

        // =========================
        // تعديل بيانات الطبيب
        // =========================

        public async Task<IActionResult> Edit(int? id)
        {

            if (id == null)
                return NotFound();



            var doctor =
                await _context.Doctors.FindAsync(id);



            if (doctor == null)
                return NotFound();



            return View(doctor);

        }






        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Doctor doctor,
            IFormFile? imageFile)
        {

            if (id != doctor.Id)
                return NotFound();




            if (ModelState.IsValid)
            {


                var oldDoctor =
                    await _context.Doctors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);




                if (imageFile != null)
                {

                    if (!string.IsNullOrEmpty(oldDoctor?.ImagePath))
                    {
                        DeleteImage(oldDoctor.ImagePath);
                    }



                    doctor.ImagePath =
                        await SaveImage(imageFile);

                }
                else
                {
                    doctor.ImagePath =
                        oldDoctor?.ImagePath;
                }




                _context.Update(doctor);

                await _context.SaveChangesAsync();



                return RedirectToAction(nameof(Index));

            }



            return View(doctor);

        }








        // =========================
        // حذف الطبيب
        // =========================

        public async Task<IActionResult> Delete(int? id)
        {

            if (id == null)
                return NotFound();



            var doctor =
                await _context.Doctors
                .FirstOrDefaultAsync(x => x.Id == id);



            if (doctor == null)
                return NotFound();



            return View(doctor);

        }





        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            var doctor =
                await _context.Doctors.FindAsync(id);



            if (doctor != null)
            {

                if (!string.IsNullOrEmpty(doctor.ImagePath))
                {
                    DeleteImage(doctor.ImagePath);
                }



                _context.Doctors.Remove(doctor);

                await _context.SaveChangesAsync();

            }



            return RedirectToAction(nameof(Index));

        }







        // =========================
        // حفظ صورة الطبيب
        // =========================

        private async Task<string> SaveImage(IFormFile imageFile)
        {

            string ext =
                Path.GetExtension(imageFile.FileName)
                .ToLower();



            string folder =
                Path.Combine(
                    _environment.WebRootPath,
                    "images",
                    "doctors"
                );



            Directory.CreateDirectory(folder);



            string fileName =
                Guid.NewGuid()
                + ext;



            string path =
                Path.Combine(folder, fileName);




            using (var stream =
                new FileStream(path, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }



            return "/images/doctors/" + fileName;

        }







        private void DeleteImage(string imagePath)
        {

            string path =
                Path.Combine(
                    _environment.WebRootPath,
                    imagePath.TrimStart('/')
                );



            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }

        }
        // =========================
        // إضافة تدريب جديد للطبيب
        // =========================

        public async Task<IActionResult> AddTraining(int id)
        {

            var doctor =
                await _context.Doctors
                .Include(x => x.TrainingRotations)
                .FirstOrDefaultAsync(x => x.Id == id);



            if (doctor == null)
                return NotFound();



            var completedDepartments =
                doctor.TrainingRotations
                .Select(x => x.DepartmentId)
                .ToList();



            TempData["CompletedDepartments"] =
                string.Join(",", completedDepartments);



            return RedirectToAction(
                "Create",
                "TrainingRotations",
                new
                {
                    doctorId = id
                });

        }







        // =========================
        // إنشاء إشعار تدريب Word
        // =========================

        public async Task<IActionResult> TrainingNotice(int id)
        {

            var doctor =
                await _context.Doctors
                .FirstOrDefaultAsync(x => x.Id == id);



            if (doctor == null)
            {
                return NotFound();
            }



            string templatePath =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Templates",
                    "WordFiles",
                    "اشعار تدريب.docx"
                );



            if (!System.IO.File.Exists(templatePath))
            {
                return Content("ملف الوورد غير موجود: " + templatePath);
            }



            string outputFile =
                Path.Combine(
                    Path.GetTempPath(),
                    $"اشعار تدريب - {doctor.الاسم}.docx"
                );



            // نسخ القالب إلى ملف مؤقت
            System.IO.File.Copy(
                templatePath,
                outputFile,
                true
            );



            // تعديل الـ Bookmarks
            using (WordprocessingDocument wordDoc =
                WordprocessingDocument.Open(outputFile, true))
            {


                var bookmarks =
                    wordDoc.MainDocumentPart!
                    .Document
                    .Body!
                    .Descendants<BookmarkStart>()
                    .ToList();



                foreach (var bookmark in bookmarks)
                {

                    if (bookmark.Name == "EmployeeName")
                    {

                        ReplaceBookmarkText(
                            bookmark,
                            doctor.الاسم
                        );

                    }



                    if (bookmark.Name == "StartDate")
                    {

                        ReplaceBookmarkText(
                            bookmark,
                            doctor.تاريخ_المباشرة?
                            .ToString("yyyy/MM/dd")
                            ?? ""
                        );

                    }

                }



                wordDoc.MainDocumentPart.Document.Save();

            }




            byte[] fileBytes =
                await System.IO.File.ReadAllBytesAsync(outputFile);



            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"اشعار تدريب - {doctor.الاسم}.docx"
            );

        }




        // =========================
        // تعبئة Bookmark في Word
        // =========================

        // =========================
        // تعبئة Bookmark مع تنسيق خاص
        // =========================

        private void ReplaceBookmarkText(
            BookmarkStart bookmark,
            string text)
        {

            var run = new Run();


            // خصائص الخط
            var runProperties = new RunProperties();


            // حجم الخط 16
            runProperties.Append(
                new FontSize
                {
                    Val = "32"
                }
            );


            // خط عريض Bold
            runProperties.Append(
                new Bold()
            );


            // لون الخط (أزرق)
            runProperties.Append(
                new Color
                {
                    Val = "0070C0"
                }
            );


            // إضافة الخصائص للنص
            run.Append(runProperties);


            // النص داخل الـ Bookmark
            run.Append(
                new Text(text)
                {
                    Space = SpaceProcessingModeValues.Preserve
                }
            );



            // وضع النص بعد الـ Bookmark
            bookmark.Parent.InsertAfter(
                run,
                bookmark
            );

        }

        // =========================
        // إنشاء إنهاء امتياز Word
        // =========================

        public async Task<IActionResult> InternshipFinish(int id)
        {
            var doctor =
                await _context.Doctors
                .FirstOrDefaultAsync(x => x.Id == id);


            if (doctor == null)
            {
                return NotFound();
            }


            string templatePath =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Templates",
                    "WordFiles",
                    "انهاء امتياز.docx"
                );


            if (!System.IO.File.Exists(templatePath))
            {
                return Content("ملف الوورد غير موجود: " + templatePath);
            }



            string outputFile =
                Path.Combine(
                    Path.GetTempPath(),
                    $"انهاء امتياز - {doctor.الاسم}.docx"
                );



            System.IO.File.Copy(
                templatePath,
                outputFile,
                true
            );



            // أول تاريخ مباشرة
            DateTime? startDate = doctor.تاريخ_المباشرة;


            // تاريخ انتهاء الامتياز = سنة ناقص يوم
            DateTime? endDate = null;

            if (startDate.HasValue)
            {
                endDate =
                    startDate.Value
                    .AddYears(1)
                    .AddDays(-1);
            }




            using (WordprocessingDocument wordDoc =
                WordprocessingDocument.Open(outputFile, true))
            {

                var bookmarks =
                    wordDoc.MainDocumentPart!
                    .Document
                    .Body!
                    .Descendants<BookmarkStart>()
                    .ToList();



                foreach (var bookmark in bookmarks)
                {

                    if (bookmark.Name == "EmployeeName")
                    {
                        ReplaceBookmarkText(
                            bookmark,
                            doctor.الاسم
                        );
                    }



                    if (bookmark.Name == "StartDate")
                    {
                        ReplaceBookmarkText(
                            bookmark,
                            startDate?
                            .ToString("yyyy/MM/dd")
                            ?? ""
                        );
                    }



                    if (bookmark.Name == "EndDate")
                    {
                        ReplaceBookmarkText(
                            bookmark,
                            endDate?
                            .ToString("yyyy/MM/dd")
                            ?? ""
                        );
                    }

                }


                wordDoc.MainDocumentPart.Document.Save();

            }



            byte[] fileBytes =
                await System.IO.File.ReadAllBytesAsync(outputFile);



            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"انهاء امتياز - {doctor.الاسم}.docx"
            );
        }

        // =========================
        // إنشاء تحديد قسم Word
        // =========================

        public async Task<IActionResult> DepartmentAssignment(int id)
        {
            var doctor =
                await _context.Doctors
                .Include(x => x.TrainingRotations)
                .ThenInclude(x => x.Department)
                .FirstOrDefaultAsync(x => x.Id == id);


            if (doctor == null)
            {
                return NotFound();
            }



            string templatePath =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Templates",
                    "WordFiles",
                    "تحديد قسم.docx"
                );



            if (!System.IO.File.Exists(templatePath))
            {
                return Content("ملف الوورد غير موجود: " + templatePath);
            }



            string outputFile =
                Path.Combine(
                    Path.GetTempPath(),
                    $"تحديد قسم - {doctor.الاسم}.docx"
                );



            System.IO.File.Copy(
                templatePath,
                outputFile,
                true
            );



            // آخر قسم تدريب للطبيب
            var currentTraining =
                doctor.TrainingRotations
                .OrderByDescending(x => x.StartDate)
                .FirstOrDefault();



            string departmentName =
                currentTraining?.Department?.Name
                ?? "";



            // بداية آخر قسم
            DateTime? startDate =
                currentTraining?.StartDate;



            // نهاية آخر قسم
            DateTime? endDate =
                currentTraining?.EndDate;




            using (WordprocessingDocument wordDoc =
                WordprocessingDocument.Open(outputFile, true))
            {

                var bookmarks =
                    wordDoc.MainDocumentPart!
                    .Document
                    .Body!
                    .Descendants<BookmarkStart>()
                    .ToList();



                foreach (var bookmark in bookmarks)
                {

                    string bookmarkName =
                        bookmark.Name?.Value ?? "";



                    if (bookmarkName == "EmployeeName")
                    {
                        ReplaceBookmarkText(
                            bookmark,
                            doctor.الاسم
                        );
                    }



                    if (bookmarkName == "startDate")
                    {
                        ReplaceBookmarkText(
                            bookmark,
                            startDate?
                            .ToString("yyyy/MM/dd")
                            ?? ""
                        );
                    }



                    if (bookmarkName == "EndDate")
                    {
                        ReplaceBookmarkText(
                            bookmark,
                            endDate?
                            .ToString("yyyy/MM/dd")
                            ?? ""
                        );
                    }



                    if (bookmarkName == "القسم")
                    {
                        ReplaceBookmarkText(
                            bookmark,
                            departmentName
                        );
                    }

                }



                wordDoc.MainDocumentPart.Document.Save();

            }




            byte[] fileBytes =
                await System.IO.File.ReadAllBytesAsync(outputFile);



            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"تحديد قسم - {doctor.الاسم}.docx"
            );
        }
    
    // =========================
// الأطباء الحاليين في قسم معين
// =========================

public async Task<IActionResult> DepartmentDoctors(string name)
        {
            if (string.IsNullOrEmpty(name))
                return RedirectToAction(nameof(Index));


            var today = DateTime.Today;


            var doctors =
                await _context.TrainingRotations
                .Include(x => x.Doctor)
                .Include(x => x.Department)
                .Where(x =>
                    x.Department.Name == name &&
                    x.StartDate.Date <= today &&
                    x.EndDate.Date >= today
                )
                .Select(x => x.Doctor)
                .Distinct()
                .ToListAsync();



            ViewBag.DepartmentName = name;


            return View(doctors);
        }
    }
}