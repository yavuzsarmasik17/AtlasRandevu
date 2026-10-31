using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TelefonBasvuruKullanimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BasvurudaKullanildiMi",
                table: "TelefonDogrulamalari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BasvurudaKullanilmaTarihi",
                table: "TelefonDogrulamalari",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasvurudaKullanildiMi",
                table: "TelefonDogrulamalari");

            migrationBuilder.DropColumn(
                name: "BasvurudaKullanilmaTarihi",
                table: "TelefonDogrulamalari");
        }
    }
}
