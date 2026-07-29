using Microsoft.AspNetCore.Mvc;
using HospitalWeb.Data;

namespace HospitalWeb.Controllers
{
    public class ImportController : Controller
    {
        private readonly AccessImporter _importer;


        public ImportController(AccessImporter importer)
        {
            _importer = importer;
        }



        // تشغيل استيراد قاعدة Access
        public async Task<IActionResult> Access()
        {
            try
            {
                var result = await _importer.Import();


                return Content(
                    "<h3 style='color:green'>"
                    + result +
                    "</h3>",
                    "text/html; charset=utf-8"
                );

            }
            catch (Exception ex)
            {

                string error = ex.ToString();



                return Content(
                    "<h3 style='color:red'>حدث خطأ أثناء الاستيراد</h3>" +
                    "<pre style='direction:ltr;text-align:left'>" +
                    error +
                    "</pre>",
                    "text/html; charset=utf-8"
                );

            }
        }

    }
}