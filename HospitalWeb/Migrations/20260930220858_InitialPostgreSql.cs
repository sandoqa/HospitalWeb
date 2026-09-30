using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HospitalWeb.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    الاسم = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    رقم_الطبيب = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    الرقم_الوطني = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ImagePath = table.Column<string>(type: "text", nullable: true),
                    الجامعة = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    سنة_التخرج = table.Column<int>(type: "integer", nullable: true),
                    تاريخ_الميلاد = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    الجنس = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    مكان_المباشرة = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    تاريخ_المباشرة = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingRotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingRotations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingRotations_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_TrainingRotations_DepartmentId",
                table: "TrainingRotations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRotations_DoctorId_DepartmentId",
                table: "TrainingRotations",
                columns: new[] { "DoctorId", "DepartmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingRotations");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Doctors");
        }
    }
}
