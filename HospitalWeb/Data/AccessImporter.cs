using System.Data.OleDb;
using HospitalWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalWeb.Data
{
    public class AccessImporter
    {

        private readonly ApplicationDbContext _context;


        private readonly string accessPath =
            @"C:\Users\My PC2\Desktop\New folder (8)\Database4.mdb";



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



            int newDoctors = 0;
            int oldDoctors = 0;
            int newRotations = 0;
            int errors = 0;


            List<string> errorDetails = new();



            try
            {

                using var connection =
                    new OleDbConnection(connectionString);


                connection.Open();



                using var command =
                    new OleDbCommand(
                        "SELECT * FROM [Sheet1]",
                        connection);



                using var reader =
                    command.ExecuteReader();



                while (reader.Read())
                {

                    try
                    {

                        string? name =
                            ReadFirstString(
                                reader,
                                "الاسم",
                                "اسم الطبيب");



                        if (string.IsNullOrWhiteSpace(name))
                            continue;


                        name = name.Trim();



                        string? doctorNumber =
                            ReadFirstString(
                                reader,
                                "رقم_الطبيب",
                                "رقم الطبيب");



                        doctorNumber =
                            doctorNumber?.Trim();





                        Doctor? doctor = null;



                        // البحث برقم الطبيب
                        if (!string.IsNullOrWhiteSpace(doctorNumber))
                        {
                            doctor =
                                await _context.Doctors
                                .FirstOrDefaultAsync(x =>
                                    x.رقم_الطبيب == doctorNumber);
                        }



                        // البحث بالاسم إذا لم يجد الرقم
                        if (doctor == null)
                        {
                            doctor =
                                await _context.Doctors
                                .FirstOrDefaultAsync(x =>
                                    x.الاسم == name);
                        }





                        if (doctor == null)
                        {

                            doctor = new Doctor
                            {

                                الاسم = name,

                                رقم_الطبيب = doctorNumber,


                                Phone =
                                ReadString(reader, "Phone"),


                                ImagePath =
                                ReadString(reader, "ImagePath")

                            };



                            _context.Doctors.Add(doctor);


                            await _context.SaveChangesAsync();


                            newDoctors++;

                        }

                        else
                        {

                            oldDoctors++;

                        }






                        newRotations += await AddRotation(
                            doctor.Id,
                            "الجراحة",
                            reader,
                            "الجراحة مباشرة",
                            "الجراحة انتهاء");



                        newRotations += await AddRotation(
                            doctor.Id,
                            "الباطني",
                            reader,
                            "الباطني مباشرة",
                            "الباطني انتهاء");



                        newRotations += await AddRotation(
                            doctor.Id,
                            "النسائية",
                            reader,
                            "النسائية مباشرة",
                            "النسائية انتهاء");



                        newRotations += await AddRotation(
                            doctor.Id,
                            "الأطفال",
                            reader,
                            "الاطفال مباشرة",
                            "الاطفال انتهاء");



                        newRotations += await AddRotation(
                            doctor.Id,
                            "الطوارئ",
                            reader,
                            "الطوارئ مباشرة",
                            "الطوارئ انتهاء");



                        newRotations += await AddRotation(
                            doctor.Id,
                            "الاختياري",
                            reader,
                            "الاختياري مباشرة",
                            "الاختياري انتهاء");


                    }
                    catch (Exception ex)
                    {

                        errors++;

                        errorDetails.Add(
                            ex.InnerException?.Message
                            ?? ex.Message);

                    }

                }





                string result =
                    "تم الاستيراد بنجاح<br/>" +

                    $"الأطباء الجدد: {newDoctors}<br/>" +

                    $"الأطباء الموجودون: {oldDoctors}<br/>" +

                    $"التدريبات المضافة: {newRotations}<br/>" +

                    $"الأخطاء: {errors}<br/><br/>";




                if (errorDetails.Count > 0)
                {

                    result += "تفاصيل الأخطاء:<br/>";


                    foreach (var e in errorDetails.Take(10))
                    {
                        result += e + "<br/>";
                    }

                }



                return result;


            }
            catch (Exception ex)
            {

                return
                "خطأ رئيسي:<br/>" +
                (ex.InnerException?.Message
                ?? ex.Message);

            }

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



            if (end < start)
                return 0;





            var department =
                await _context.Departments
                .FirstOrDefaultAsync(x =>
                    x.Name == departmentName);



            if (department == null)
                return 0;






            // منع تكرار نفس القسم للطبيب
            bool exists =
                await _context.TrainingRotations
                .AnyAsync(x =>
                    x.DoctorId == doctorId &&
                    x.DepartmentId == department.Id);



            if (exists)
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

            var value = ReadValue(reader, column);



            if (value == null || value == DBNull.Value)
                return null;



            return value.ToString();

        }







        private string? ReadFirstString(
            OleDbDataReader reader,
            params string[] columns)
        {

            foreach (var c in columns)
            {

                var value = ReadString(reader, c);


                if (!string.IsNullOrWhiteSpace(value))
                    return value;

            }


            return null;

        }







        private DateTime? GetDate(object? value)
        {

            if (value == null || value == DBNull.Value)
                return null;



            if (DateTime.TryParse(
                value.ToString(),
                out DateTime date))
                return date;



            return null;

        }


    }
}