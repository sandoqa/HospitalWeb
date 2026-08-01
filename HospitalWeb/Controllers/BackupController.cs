using Microsoft.AspNetCore.Mvc;
using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace HospitalWeb.Controllers
{
    public class BackupController : Controller
    {
        private readonly ApplicationDbContext _db;


        public BackupController(ApplicationDbContext db)
        {
            _db = db;
        }



        // تحميل نسخة احتياطية
        public IActionResult Download()
        {
            string dbPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "App_Data",
                "hospital.db"
            );


            if (!System.IO.File.Exists(dbPath))
                return NotFound();


            string fileName =
                $"hospital_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";


            return PhysicalFile(
                dbPath,
                "application/octet-stream",
                fileName
            );
        }




        // صفحة الاسترجاع
        [HttpGet]
        public IActionResult Restore()
        {
            return View();
        }





        // تنفيذ الاسترجاع
        [HttpPost]
        public async Task<IActionResult> Restore(IFormFile backupFile)
        {

            if (backupFile == null || backupFile.Length == 0)
            {
                ViewBag.Message = "لم يتم اختيار ملف";
                return View();
            }



            string appData = Path.Combine(
                Directory.GetCurrentDirectory(),
                "App_Data"
            );


            Directory.CreateDirectory(appData);



            string tempDb = Path.Combine(
                appData,
                "restore_temp.db"
            );



            // حفظ ملف النسخة الاحتياطية مؤقتاً
            using (var stream = new FileStream(
                tempDb,
                FileMode.Create,
                FileAccess.Write))
            {
                await backupFile.CopyToAsync(stream);
            }



            try
            {

                // إغلاق الاتصال الحالي
                await _db.Database.CloseConnectionAsync();



                // فتح الاتصال
                await _db.Database.OpenConnectionAsync();



                var connection =
                    _db.Database.GetDbConnection();



                using var command =
                    connection.CreateCommand();



                command.CommandText = $@"

ATTACH DATABASE '{tempDb}' AS backupdb;


DELETE FROM TrainingRotations;

INSERT INTO TrainingRotations
SELECT * FROM backupdb.TrainingRotations;


DELETE FROM Doctors;

INSERT INTO Doctors
SELECT * FROM backupdb.Doctors;


DELETE FROM Departments;

INSERT INTO Departments
SELECT * FROM backupdb.Departments;


DETACH DATABASE backupdb;

";



                await command.ExecuteNonQueryAsync();



                await _db.Database.CloseConnectionAsync();



                ViewBag.Message =
                    "تم استرجاع النسخة الاحتياطية بنجاح";


            }
            catch (Exception ex)
            {

                ViewBag.Message =
                    "حدث خطأ: " + ex.Message;

            }
            finally
            {

                if (System.IO.File.Exists(tempDb))
                {
                    System.IO.File.Delete(tempDb);
                }

            }



            return View();
        }

    }
}