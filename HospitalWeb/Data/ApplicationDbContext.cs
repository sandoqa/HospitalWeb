using Microsoft.EntityFrameworkCore;
using HospitalWeb.Models;

namespace HospitalWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }


        public DbSet<Doctor> Doctors { get; set; }

        public DbSet<Department> Departments { get; set; }

        public DbSet<TrainingRotation> TrainingRotations { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);



            // منع تكرار نفس الطبيب في نفس القسم
            modelBuilder.Entity<TrainingRotation>()
                .HasIndex(x => new { x.DoctorId, x.DepartmentId })
                .IsUnique();



            // الأقسام الافتراضية
            modelBuilder.Entity<Department>()
                .HasData(

                    new Department
                    {
                        Id = 1,
                        Name = "الجراحة"
                    },

                    new Department
                    {
                        Id = 2,
                        Name = "الباطني"
                    },

                    new Department
                    {
                        Id = 3,
                        Name = "النسائية"
                    },

                    new Department
                    {
                        Id = 4,
                        Name = "الأطفال"
                    },

                    new Department
                    {
                        Id = 5,
                        Name = "الطوارئ"
                    },

                    new Department
                    {
                        Id = 6,
                        Name = "الاختياري"
                    }

                );
        }
    }
}