using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IsletmeFotografGalerisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IsletmeFotograflari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsletmeId = table.Column<int>(type: "int", nullable: false),
                    DosyaYolu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    YuklenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsletmeFotograflari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsletmeFotograflari_Isletmeler_IsletmeId",
                        column: x => x.IsletmeId,
                        principalTable: "Isletmeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeFotograflari_IsletmeId_Sira",
                table: "IsletmeFotograflari",
                columns: new[] { "IsletmeId", "Sira" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IsletmeFotograflari");
        }
    }
}
