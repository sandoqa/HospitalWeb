using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HospitalWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueDoctorDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainingRotations_DoctorId",
                table: "TrainingRotations");

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRotations_DoctorId_DepartmentId",
                table: "TrainingRotations",
                columns: new[] { "DoctorId", "DepartmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainingRotations_DoctorId_DepartmentId",
                table: "TrainingRotations");

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "الجراحة" },
                    { 2, "الباطني" },
                    { 3, "النسائية" },
                    { 4, "الأطفال" },
                    { 5, "الطوارئ" },
                    { 6, "الاختياري" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRotations_DoctorId",
                table: "TrainingRotations",
                column: "DoctorId");
        }
    }
}
