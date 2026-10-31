using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IsletmeProfilAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hakkimizda",
                table: "Isletmeler",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramAdresi",
                table: "Isletmeler",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KapakFotografiYolu",
                table: "Isletmeler",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoYolu",
                table: "Isletmeler",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProfilGuncellemeTarihi",
                table: "Isletmeler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slogan",
                table: "Isletmeler",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebSitesi",
                table: "Isletmeler",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hakkimizda",
                table: "Isletmeler");

            migrationBuilder.DropColumn(
                name: "InstagramAdresi",
                table: "Isletmeler");

            migrationBuilder.DropColumn(
                name: "KapakFotografiYolu",
                table: "Isletmeler");

            migrationBuilder.DropColumn(
                name: "LogoYolu",
                table: "Isletmeler");

            migrationBuilder.DropColumn(
                name: "ProfilGuncellemeTarihi",
                table: "Isletmeler");

            migrationBuilder.DropColumn(
                name: "Slogan",
                table: "Isletmeler");

            migrationBuilder.DropColumn(
                name: "WebSitesi",
                table: "Isletmeler");
        }
    }
}
