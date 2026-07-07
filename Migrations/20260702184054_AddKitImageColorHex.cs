using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Migrations
{
    /// <inheritdoc />
    public partial class AddKitImageColorHex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "KitImages",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "KitImages");
        }
    }
}
