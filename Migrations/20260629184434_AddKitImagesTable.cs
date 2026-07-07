using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Migrations
{
    /// <inheritdoc />
    public partial class AddKitImagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KitId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitImages_Kits_KitId",
                        column: x => x.KitId,
                        principalTable: "Kits",
                        principalColumn: "KitId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitImages_KitId_SortOrder",
                table: "KitImages",
                columns: new[] { "KitId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitImages");
        }
    }
}
