using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelefonDogrulama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelefonDogrulamalari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Telefon = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KodOzeti = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SonGecerlilikTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HataliDenemeSayisi = table.Column<int>(type: "int", nullable: false),
                    KullanildiMi = table.Column<bool>(type: "bit", nullable: false),
                    DogrulamaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelefonDogrulamalari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelefonDogrulamalari_Telefon_SonGecerlilikTarihi",
                table: "TelefonDogrulamalari",
                columns: new[] { "Telefon", "SonGecerlilikTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelefonDogrulamalari");
        }
    }
}
