using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RandevuVeYorumAltyapisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hizmetler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Fiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SureDakika = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hizmetler", x => x.Id);
                    table.CheckConstraint("CK_Hizmetler_Fiyat", "[Fiyat] >= 0");
                    table.CheckConstraint("CK_Hizmetler_SureDakika", "[SureDakika] > 0");
                    table.ForeignKey(
                        name: "FK_Hizmetler_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Randevular",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    SubeId = table.Column<int>(type: "int", nullable: false),
                    HizmetId = table.Column<int>(type: "int", nullable: false),
                    MusteriKullaniciId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MusteriNotu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsletmeAciklamasi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OnayTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TamamlanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Randevular", x => x.Id);
                    table.CheckConstraint("CK_Randevular_Durum", "[Durum] IN ('OnayBekliyor','Onaylandi','Reddedildi','IptalEdildi','Tamamlandi')");
                    table.CheckConstraint("CK_Randevular_Tarih", "[BitisTarihi] > [BaslangicTarihi]");
                    table.ForeignKey(
                        name: "FK_Randevular_AspNetUsers_MusteriKullaniciId",
                        column: x => x.MusteriKullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Randevular_Hizmetler_HizmetId",
                        column: x => x.HizmetId,
                        principalTable: "Hizmetler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Randevular_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Randevular_Subeler_SubeId",
                        column: x => x.SubeId,
                        principalTable: "Subeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MusteriYorumlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    RandevuId = table.Column<int>(type: "int", nullable: false),
                    MusteriKullaniciId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Puan = table.Column<int>(type: "int", nullable: false),
                    Yorum = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsletmeCevabi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CevapTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YayinlandiMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusteriYorumlari", x => x.Id);
                    table.CheckConstraint("CK_MusteriYorumlari_Puan", "[Puan] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_MusteriYorumlari_AspNetUsers_MusteriKullaniciId",
                        column: x => x.MusteriKullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MusteriYorumlari_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MusteriYorumlari_Randevular_RandevuId",
                        column: x => x.RandevuId,
                        principalTable: "Randevular",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hizmetler_IsletmeId",
                table: "Hizmetler",
                column: "IsletmeId");

            migrationBuilder.CreateIndex(
                name: "IX_MusteriYorumlari_IsletmeId_YayinlandiMi",
                table: "MusteriYorumlari",
                columns: new[] { "IsletmeId", "YayinlandiMi" });

            migrationBuilder.CreateIndex(
                name: "IX_MusteriYorumlari_MusteriKullaniciId",
                table: "MusteriYorumlari",
                column: "MusteriKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_MusteriYorumlari_RandevuId",
                table: "MusteriYorumlari",
                column: "RandevuId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Randevular_HizmetId",
                table: "Randevular",
                column: "HizmetId");

            migrationBuilder.CreateIndex(
                name: "IX_Randevular_IsletmeId",
                table: "Randevular",
                column: "IsletmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Randevular_MusteriKullaniciId_BaslangicTarihi",
                table: "Randevular",
                columns: new[] { "MusteriKullaniciId", "BaslangicTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_Randevular_SubeId_BaslangicTarihi_BitisTarihi",
                table: "Randevular",
                columns: new[] { "SubeId", "BaslangicTarihi", "BitisTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MusteriYorumlari");

            migrationBuilder.DropTable(
                name: "Randevular");

            migrationBuilder.DropTable(
                name: "Hizmetler");
        }
    }
}
