using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSoftDeleteAndSwitchImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Switches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Switches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Kits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Keycaps",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Switches");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Switches");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Kits");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Keycaps");
        }
    }
}
