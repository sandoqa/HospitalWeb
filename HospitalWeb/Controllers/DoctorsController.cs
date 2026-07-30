using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalWeb.Data;
using HospitalWeb.Models;

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



            // البحث بالاسم أو رقم الهاتف
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();


                doctors = doctors.Where(x =>

                    x.الاسم.Contains(search)

                    ||

                    (x.Phone != null &&
                     x.Phone.Contains(search))

                );
            }



            var result = await doctors
                .OrderBy(x => x.الاسم)
                .ToListAsync();



            Dictionary<int, int> warningDays = new();



            foreach (var doctor in result)
            {

                int days = GetWarningDays(doctor);


                if (days >= 1 && days <= 5)
                {
                    warningDays[doctor.Id] = days;
                }

            }



            // ترتيب حسب قرب انتهاء التدريب
            result = result
                .OrderBy(x =>
                    warningDays.ContainsKey(x.Id)
                    ? warningDays[x.Id]
                    : 999)
                .ThenBy(x => x.الاسم)
                .ToList();



            ViewBag.WarningDays = warningDays;

            ViewBag.Search = search;



            return View(result);

        }





        // =========================
        // حساب الأيام المتبقية
        // =========================
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



            bool hasNextRotation =
                doctor.TrainingRotations.Any(x =>
                    x.StartDate.Date >
                    currentRotation.EndDate.Date);



            if (hasNextRotation)
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

            if (ModelState.IsValid)
            {

                if (imageFile != null)
                {
                    doctor.ImagePath =
                        await SaveImage(imageFile);
                }



                _context.Doctors.Add(doctor);

                await _context.SaveChangesAsync();


                return RedirectToAction(nameof(Index));
            }



            return View(doctor);

        }        // =========================
        // تعديل
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
        // حذف
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

    }
}