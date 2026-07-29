using System.ComponentModel.DataAnnotations;

namespace HospitalWeb.Models
{
    public class TrainingRotation
    {
        public int Id { get; set; }



        // الطبيب
        [Display(Name = "الطبيب")]
        [Required(ErrorMessage = "اختر الطبيب")]
        public int DoctorId { get; set; }


        public Doctor? Doctor { get; set; }





        // القسم
        [Display(Name = "القسم")]
        [Required(ErrorMessage = "اختر القسم")]
        public int DepartmentId { get; set; }


        public Department? Department { get; set; }







        // تاريخ البداية
        [Display(Name = "بداية التدريب")]
        [DataType(DataType.Date)]
        [DisplayFormat(
            DataFormatString = "{0:dd/MM/yyyy}",
            ApplyFormatInEditMode = true)]
        [Required(ErrorMessage = "أدخل تاريخ البداية")]
        public DateTime StartDate { get; set; }







        // تاريخ النهاية
        [Display(Name = "نهاية التدريب")]
        [DataType(DataType.Date)]
        [DisplayFormat(
            DataFormatString = "{0:dd/MM/yyyy}",
            ApplyFormatInEditMode = true)]
        [Required(ErrorMessage = "أدخل تاريخ النهاية")]
        public DateTime EndDate { get; set; }








        // الأيام المتبقية
        [Display(Name = "الأيام المتبقية")]
        public int RemainingDays
        {
            get
            {
                int days =
                    (EndDate.Date - DateTime.Today).Days;


                return days < 0 ? 0 : days;
            }
        }

    }
}