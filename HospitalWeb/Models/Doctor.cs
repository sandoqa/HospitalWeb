using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalWeb.Models
{
    [Table("Doctors")]
    public class Doctor
    {

        [Key]
        public int Id { get; set; }



        [Column("الاسم")]
        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم الطبيب")]
        public string الاسم { get; set; } = "";



        [Column("رقم_الطبيب")]
        [StringLength(20)]
        [Display(Name = "رقم الطبيب")]
        public string? رقم_الطبيب { get; set; }



        [Column("الرقم_الوطني")]
        [StringLength(20)]
        [Display(Name = "الرقم الوطني")]
        public string? الرقم_الوطني { get; set; }



        [Column("Phone")]
        [StringLength(20)]
        [Display(Name = "رقم الجوال")]
        public string? Phone { get; set; }



        [Column("ImagePath")]
        [Display(Name = "صورة الطبيب")]
        public string? ImagePath { get; set; }



        [Column("الجامعة")]
        [StringLength(100)]
        [Display(Name = "الجامعة")]
        public string? الجامعة { get; set; }



        [Column("سنة_التخرج")]
        [Display(Name = "سنة التخرج")]
        public int? سنة_التخرج { get; set; }



        [Column("تاريخ_الميلاد")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ الميلاد")]
        public DateTime? تاريخ_الميلاد { get; set; }



        [Column("الجنس")]
        [StringLength(10)]
        [Display(Name = "الجنس")]
        public string? الجنس { get; set; }




        // موجود في Access
        [Column("مكان_المباشرة")]
        [StringLength(100)]
        [Display(Name = "مكان المباشرة")]
        public string? مكان_المباشرة { get; set; }




        // موجود في Access
        [Column("تاريخ_المباشرة")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ المباشرة")]
        public DateTime? تاريخ_المباشرة { get; set; }





        // العلاقة مع جدول التدريب
        public virtual ICollection<TrainingRotation> TrainingRotations { get; set; }
            = new List<TrainingRotation>();

    }
}