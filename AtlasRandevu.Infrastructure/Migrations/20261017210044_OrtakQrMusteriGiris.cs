using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrtakQrMusteriGiris : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Randevular_Durum",
                table: "Randevular");

            migrationBuilder.AddColumn<bool>(
                name: "GelisDogrulandiMi",
                table: "Randevular",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "GelisTarihi",
                table: "Randevular",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GelisYontemi",
                table: "Randevular",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Randevular_MusteriKullaniciId_Durum_BaslangicTarihi",
                table: "Randevular",
                columns: new[] { "MusteriKullaniciId", "Durum", "BaslangicTarihi" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Randevular_Durum",
                table: "Randevular",
                sql: "[Durum] IN ('OnayBekliyor','Onaylandi','Reddedildi','IptalEdildi','Geldi','Gelmedi','Tamamlandi')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Randevular_MusteriKullaniciId_Durum_BaslangicTarihi",
                table: "Randevular");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Randevular_Durum",
                table: "Randevular");

            migrationBuilder.DropColumn(
                name: "GelisDogrulandiMi",
                table: "Randevular");

            migrationBuilder.DropColumn(
                name: "GelisTarihi",
                table: "Randevular");

            migrationBuilder.DropColumn(
                name: "GelisYontemi",
                table: "Randevular");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Randevular_Durum",
                table: "Randevular",
                sql: "[Durum] IN ('OnayBekliyor','Onaylandi','Reddedildi','IptalEdildi','Tamamlandi')");
        }
    }
}
