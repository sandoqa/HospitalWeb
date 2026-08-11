using System.Data.OleDb;
using HospitalWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalWeb.Data
{
    public class AccessImporter
    {
        private readonly ApplicationDbContext _context;

        private readonly string accessPath =
            @"C:\Database\Database4.mdb";


        public AccessImporter(ApplicationDbContext context)
        {
            _context = context;
        }



        public async Task<string> Import()
        {

            if (!File.Exists(accessPath))
                return "ملف Access غير موجود";


            string connectionString =
                $@"Provider=Microsoft.ACE.OLEDB.12.0;
                Data Source={accessPath};
                Mode=Read;";


            int addedDoctors = 0;
            int updatedDoctors = 0;
            int deletedDoctors = 0;
            int rotationsAdded = 0;
            int errors = 0;


            List<string> accessNumbers = new();



            try
            {

                using var connection =
                    new OleDbConnection(connectionString);


                connection.Open();



                // قراءة أرقام الأطباء من Access

                using (var readCommand =
                    new OleDbCommand(
                        "SELECT [الرقم] FROM [Sheet1]",
                        connection))
                {

                    using var numberReader =
                        readCommand.ExecuteReader();


                    while (numberReader.Read())
                    {

                        string? number =
                            numberReader[0]?.ToString();


                        if (!string.IsNullOrWhiteSpace(number))
                        {

                            string cleanNumber =
                                number.Trim();


                            if (!accessNumbers.Contains(cleanNumber))
                            {
                                accessNumbers.Add(cleanNumber);
                            }

                        }

                    }

                }



                Console.WriteLine(
                    "عدد أرقام Access الفريدة: "
                    + accessNumbers.Count);



                // أرقام موجودة في قاعدة البرنامج

                var programNumbers =
                    await _context.Doctors
                    .Where(x => x.رقم_الطبيب != null)
                    .Select(x => x.رقم_الطبيب!)
                    .ToListAsync();



                programNumbers =
                    programNumbers
                    .Select(x => x.Trim())
                    .ToList();



                // مقارنة Access مع البرنامج

                var accessOnlyDoctors =
                    accessNumbers
                    .Where(x => !programNumbers.Contains(x))
                    .ToList();



                Console.WriteLine(
                    "الأطباء الموجودون في Access فقط: "
                    + accessOnlyDoctors.Count);



                foreach (var n in accessOnlyDoctors)
                {
                    Console.WriteLine(
                        "رقم موجود في Access فقط: "
                        + n);
                }



                // عدد الأطباء في البرنامج

                Console.WriteLine(
                    "عدد الأطباء في البرنامج قبل الاستيراد: "
                    + programNumbers.Count);
                // قراءة بيانات الأطباء وتحديثها أو إضافتها

                using var dataCommand =
                    new OleDbCommand(
                        "SELECT * FROM [Sheet1]",
                        connection);



                using var dataReader =
                    dataCommand.ExecuteReader();



                while (dataReader.Read())
                {

                    try
                    {

                        string? number =
                            ReadString(
                                dataReader,
                                "الرقم");


                        string? name =
                            ReadString(
                                dataReader,
                                "الاسم");



                        if (string.IsNullOrWhiteSpace(number) ||
    string.IsNullOrWhiteSpace(name))
                        {
                            Console.WriteLine(
                                "تم تجاهل سجل ناقص - الرقم: "
                                + number
                                + " الاسم: "
                                + name);

                            continue;
                        }



                        number = number.Trim();
                        name = name.Trim();



                        Doctor? doctor =
                            await _context.Doctors
                            .FirstOrDefaultAsync(x =>
                                x.رقم_الطبيب == number);



                        if (doctor == null)
                        {

                            doctor = new Doctor
                            {

                                الاسم = name,

                                رقم_الطبيب = number,


                                Phone =
                                    ReadString(
                                        dataReader,
                                        "Phone"),


                                ImagePath =
                                    ReadString(
                                        dataReader,
                                        "ImagePath")

                            };


                            _context.Doctors.Add(doctor);


                            await _context.SaveChangesAsync();


                            addedDoctors++;

                        }
                        else
                        {

                            doctor.الاسم = name;


                            doctor.Phone =
                                ReadString(
                                    dataReader,
                                    "Phone");



                            doctor.ImagePath =
                                ReadString(
                                    dataReader,
                                    "ImagePath");



                            // حذف تدريبات الطبيب القديمة

                            var oldRotations =
                                await _context.TrainingRotations
                                .Where(x =>
                                    x.DoctorId == doctor.Id)
                                .ToListAsync();



                            _context.TrainingRotations
                                .RemoveRange(oldRotations);



                            await _context.SaveChangesAsync();



                            updatedDoctors++;

                        }



                        rotationsAdded +=
                            await AddRotations(
                                doctor.Id,
                                dataReader);



                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine(
                            "خطأ في سجل: "
                            + ex.Message);


                        errors++;

                    }

                }



                // حفظ أي تغييرات متبقية

                await _context.SaveChangesAsync();



                Console.WriteLine(
                    "انتهت قراءة بيانات الأطباء");
                // النتيجة النهائية

                string result =
                    "تمت مزامنة Access بالكامل<br/><br/>" +

                    $"الأطباء الجدد: {addedDoctors}<br/>" +

                    $"الأطباء المحدثون: {updatedDoctors}<br/>" +

                    $"الأطباء المحذوفون: {deletedDoctors}<br/>" +

                    $"التدريبات المضافة: {rotationsAdded}<br/>" +

                    $"الأخطاء: {errors}";


                result +=
                    "<br/>عدد الأطباء في Access: "
                    + accessNumbers.Count;



                result +=
                    "<br/>عدد الأطباء في البرنامج: "
                    + await _context.Doctors.CountAsync();



                return result;


            }
            catch (Exception ex)
            {

                return
                    "خطأ: " +
                    (ex.InnerException?.Message
                    ?? ex.Message);

            }

        }





        private async Task<int> AddRotations(
            int doctorId,
            OleDbDataReader reader)
        {

            int count = 0;



            count += await AddRotation(
                doctorId,
                "الجراحة",
                reader,
                "الجراحة مباشرة",
                "الجراحة انتهاء");



            count += await AddRotation(
                doctorId,
                "الباطني",
                reader,
                "الباطني مباشرة",
                "الباطني انتهاء");



            count += await AddRotation(
                doctorId,
                "النسائية",
                reader,
                "النسائية مباشرة",
                "النسائية انتهاء");



            count += await AddRotation(
                doctorId,
                "الأطفال",
                reader,
                "الاطفال مباشرة",
                "الاطفال انتهاء");



            count += await AddRotation(
                doctorId,
                "الطوارئ",
                reader,
                "الطوارئ مباشرة",
                "الطوارئ انتهاء");



            count += await AddRotation(
                doctorId,
                "الاختياري",
                reader,
                "الاختياري مباشرة",
                "الاختياري انتهاء");



            return count;

        }
        private async Task<int> AddRotation(
            int doctorId,
            string departmentName,
            OleDbDataReader reader,
            string startColumn,
            string endColumn)
        {

            DateTime? start =
                GetDate(
                    ReadValue(reader, startColumn));



            DateTime? end =
                GetDate(
                    ReadValue(reader, endColumn));



            if (start == null || end == null)
                return 0;



            var department =
                await _context.Departments
                .FirstOrDefaultAsync(x =>
                    x.Name == departmentName);



            if (department == null)
                return 0;



            _context.TrainingRotations.Add(
                new TrainingRotation
                {

                    DoctorId = doctorId,

                    DepartmentId = department.Id,

                    StartDate = start.Value,

                    EndDate = end.Value

                });



            await _context.SaveChangesAsync();



            return 1;

        }





        private object? ReadValue(
            OleDbDataReader reader,
            string column)
        {
            try
            {
                return reader[column];
            }
            catch
            {
                return null;
            }
        }





        private string? ReadString(
            OleDbDataReader reader,
            string column)
        {

            var value =
                ReadValue(reader, column);



            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }



            return value.ToString();

        }





        private DateTime? GetDate(object? value)
        {

            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }



            if (DateTime.TryParse(
                value.ToString(),
                out DateTime date))
            {
                return date;
            }



            return null;

        }


    }
}