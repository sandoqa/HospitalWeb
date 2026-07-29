using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorStartInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "تاريخ_المباشرة",
                table: "Doctors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "مكان_المباشرة",
                table: "Doctors",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "تاريخ_المباشرة",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "مكان_المباشرة",
                table: "Doctors");
        }
    }
}
