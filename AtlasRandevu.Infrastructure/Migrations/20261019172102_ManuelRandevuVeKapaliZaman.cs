using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ManuelRandevuVeKapaliZaman : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MusteriKullaniciId",
                table: "Randevular",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<string>(
                name: "ManuelMusteriAdi",
                table: "Randevular",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManuelMusteriTelefonu",
                table: "Randevular",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RandevuKaynagi",
                table: "Randevular",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Online");

            migrationBuilder.CreateTable(
                name: "PersonelKapaliZamanlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    SubeId = table.Column<int>(type: "int", nullable: false),
                    PersonelId = table.Column<int>(type: "int", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonelKapaliZamanlar", x => x.Id);
                    table.CheckConstraint("CK_PersonelKapaliZamanlar_Tarih", "[BitisTarihi] > [BaslangicTarihi]");
                    table.ForeignKey(
                        name: "FK_PersonelKapaliZamanlar_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonelKapaliZamanlar_Personeller_PersonelId",
                        column: x => x.PersonelId,
                        principalTable: "Personeller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonelKapaliZamanlar_Subeler_SubeId",
                        column: x => x.SubeId,
                        principalTable: "Subeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonelKapaliZamanlar_IsletmeId",
                table: "PersonelKapaliZamanlar",
                column: "IsletmeId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonelKapaliZamanlar_PersonelId_BaslangicTarihi_BitisTarihi",
                table: "PersonelKapaliZamanlar",
                columns: new[] { "PersonelId", "BaslangicTarihi", "BitisTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonelKapaliZamanlar_SubeId",
                table: "PersonelKapaliZamanlar",
                column: "SubeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonelKapaliZamanlar");

            migrationBuilder.DropColumn(
                name: "ManuelMusteriAdi",
                table: "Randevular");

            migrationBuilder.DropColumn(
                name: "ManuelMusteriTelefonu",
                table: "Randevular");

            migrationBuilder.DropColumn(
                name: "RandevuKaynagi",
                table: "Randevular");

            migrationBuilder.AlterColumn<string>(
                name: "MusteriKullaniciId",
                table: "Randevular",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
