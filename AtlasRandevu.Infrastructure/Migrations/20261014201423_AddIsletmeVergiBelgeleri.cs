using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsletmeVergiBelgeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IsletmeBelgeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    BelgeTuru = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DosyaYolu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IcerikTuru = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    YuklemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsletmeBelgeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsletmeBelgeleri_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsletmeVergiBilgileri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    VergiNumarasi = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    VergiDairesi = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TicariUnvan = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsletmeVergiBilgileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsletmeVergiBilgileri_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BelgeDogrulamalari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelgeId = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OcrSonucu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BilgilerEslesiyorMu = table.Column<bool>(type: "bit", nullable: true),
                    ResmiDogrulamaDurumu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KontrolTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BelgeDogrulamalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BelgeDogrulamalari_IsletmeBelgeleri_BelgeId",
                        column: x => x.BelgeId,
                        principalTable: "IsletmeBelgeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BelgeDogrulamalari_BelgeId",
                table: "BelgeDogrulamalari",
                column: "BelgeId");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeBelgeleri_IsletmeId",
                table: "IsletmeBelgeleri",
                column: "IsletmeId");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeVergiBilgileri_IsletmeId",
                table: "IsletmeVergiBilgileri",
                column: "IsletmeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BelgeDogrulamalari");

            migrationBuilder.DropTable(
                name: "IsletmeVergiBilgileri");

            migrationBuilder.DropTable(
                name: "IsletmeBelgeleri");
        }
    }
}
