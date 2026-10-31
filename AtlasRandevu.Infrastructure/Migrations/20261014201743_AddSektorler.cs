using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AtlasRandevu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSektorler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sektorler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sektorler", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Sektorler",
                columns: new[] { "Id", "Ad", "AktifMi", "Sira", "Slug" },
                values: new object[,]
                {
                    { 1, "Erkek Berberi", true, 1, "erkek-berberi" },
                    { 2, "Kadın Kuaförü", true, 2, "kadin-kuaforu" },
                    { 3, "Güzellik Salonu", true, 3, "guzellik-salonu" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Isletmeler_SektorId",
                table: "Isletmeler",
                column: "SektorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sektorler_Slug",
                table: "Sektorler",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Isletmeler_Sektorler_SektorId",
                table: "Isletmeler",
                column: "SektorId",
                principalTable: "Sektorler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Isletmeler_Sektorler_SektorId",
                table: "Isletmeler");

            migrationBuilder.DropTable(
                name: "Sektorler");

            migrationBuilder.DropIndex(
                name: "IX_Isletmeler_SektorId",
                table: "Isletmeler");
        }
    }
}
