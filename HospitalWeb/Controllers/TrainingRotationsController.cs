using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HospitalWeb.Data;
using HospitalWeb.Models;

namespace HospitalWeb.Controllers
{
    public class TrainingRotationsController : Controller
    {

        private readonly ApplicationDbContext _context;


        public TrainingRotationsController(ApplicationDbContext context)
        {
            _context = context;
        }




        // =========================
        // جميع التدريبات
        // =========================
        public async Task<IActionResult> Index()
        {

            var rotations =
                await _context.TrainingRotations

                .Include(x => x.Doctor)

                .Include(x => x.Department)

                .OrderBy(x => x.StartDate)

                .ToListAsync();



            return View(rotations);
        }





        // =========================
        // تفاصيل التدريب
        // =========================
        public async Task<IActionResult> Details(int? id)
        {

            if (id == null)
                return NotFound();



            var rotation =
                await _context.TrainingRotations

                .Include(x => x.Doctor)

                .Include(x => x.Department)

                .FirstOrDefaultAsync(x => x.Id == id);



            if (rotation == null)
                return NotFound();



            return View(rotation);
        }





        // =========================
        // إضافة تدريب جديد
        // =========================
        public async Task<IActionResult> Create(int? doctorId)
        {

            if (doctorId == null)
                return NotFound();



            var doctor =
                await _context.Doctors

                .Include(x => x.TrainingRotations)

                .FirstOrDefaultAsync(x => x.Id == doctorId);



            if (doctor == null)
                return NotFound();




            var usedDepartments =
                doctor.TrainingRotations

                .Select(x => x.DepartmentId)

                .ToList();




            LoadLists(
                doctorId,
                null,
                usedDepartments
            );



            return View(
                new TrainingRotation
                {
                    DoctorId = doctorId.Value
                }
            );

        }







        // =========================
        // حفظ التدريب
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            TrainingRotation trainingRotation)
        {


            if (ModelState.IsValid)
            {


                bool exists =
                    await _context.TrainingRotations

                    .AnyAsync(x =>
                        x.DoctorId ==
                        trainingRotation.DoctorId

                        &&

                        x.DepartmentId ==
                        trainingRotation.DepartmentId
                    );



                if (exists)
                {

                    ModelState.AddModelError(
                        "",
                        "هذا الطبيب أنهى التدريب في هذا القسم مسبقاً"
                    );

                }

                else
                {

                    _context.TrainingRotations
                        .Add(trainingRotation);


                    await _context.SaveChangesAsync();


                    return RedirectToAction(
                        "Details",
                        "Doctors",
                        new
                        {
                            id = trainingRotation.DoctorId
                        });

                }

            }




            var completed =
                await _context.TrainingRotations

                .Where(x =>
                    x.DoctorId ==
                    trainingRotation.DoctorId)

                .Select(x =>
                    x.DepartmentId)

                .ToListAsync();




            LoadLists(
                trainingRotation.DoctorId,
                trainingRotation.DepartmentId,
                completed
            );



            return View(trainingRotation);

        }








        // =========================
        // تعديل التدريب
        // =========================
        public async Task<IActionResult> Edit(int? id)
        {

            if (id == null)
                return NotFound();



            var rotation =
                await _context.TrainingRotations
                .FindAsync(id);



            if (rotation == null)
                return NotFound();



            LoadLists(
                rotation.DoctorId,
                rotation.DepartmentId
            );



            return View(rotation);

        }








        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TrainingRotation trainingRotation)
        {

            if (id != trainingRotation.Id)
                return NotFound();



            if (ModelState.IsValid)
            {

                _context.Update(trainingRotation);

                await _context.SaveChangesAsync();


                return RedirectToAction(
                    "Details",
                    "Doctors",
                    new
                    {
                        id = trainingRotation.DoctorId
                    });

            }



            LoadLists(
                trainingRotation.DoctorId,
                trainingRotation.DepartmentId
            );



            return View(trainingRotation);

        }








        // =========================
        // حذف
        // =========================
        public async Task<IActionResult> Delete(int? id)
        {

            if (id == null)
                return NotFound();



            var rotation =
                await _context.TrainingRotations

                .Include(x => x.Doctor)

                .Include(x => x.Department)

                .FirstOrDefaultAsync(x => x.Id == id);



            if (rotation == null)
                return NotFound();



            return View(rotation);

        }






        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
        {

            var rotation =
                await _context.TrainingRotations
                .FindAsync(id);



            if (rotation != null)
            {

                _context.TrainingRotations
                    .Remove(rotation);


                await _context.SaveChangesAsync();

            }



            return RedirectToAction(
                "Index");

        }









        // =========================
        // تعبئة القوائم
        // =========================
        private void LoadLists(
            int? doctorId = null,
            int? departmentId = null,
            List<int>? excludedDepartments = null)
        {



            ViewData["DoctorId"] =
                new SelectList(
                    _context.Doctors
                    .OrderBy(x => x.الاسم),
                    "Id",
                    "الاسم",
                    doctorId
                );





            var departments =
                _context.Departments
                .AsQueryable();





            if (excludedDepartments != null &&
                excludedDepartments.Count > 0)
            {

                departments =
                    departments
                    .Where(x =>
                        !excludedDepartments
                        .Contains(x.Id));

            }





            ViewData["DepartmentId"] =
                new SelectList(
                    departments
                    .OrderBy(x => x.Id),
                    "Id",
                    "Name",
                    departmentId
                );

        }






        private bool TrainingRotationExists(int id)
        {
            return _context.TrainingRotations
                .Any(x => x.Id == id);
        }


    }
}