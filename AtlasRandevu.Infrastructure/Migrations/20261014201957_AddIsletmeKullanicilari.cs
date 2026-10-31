using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsletmeKullanicilari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IsletmeKullanicilari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    KullaniciId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsletmeKullanicilari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsletmeKullanicilari_AspNetUsers_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsletmeKullanicilari_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeKullanicilari_IsletmeId_KullaniciId",
                table: "IsletmeKullanicilari",
                columns: new[] { "IsletmeId", "KullaniciId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeKullanicilari_KullaniciId",
                table: "IsletmeKullanicilari",
                column: "KullaniciId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IsletmeKullanicilari");
        }
    }
}
