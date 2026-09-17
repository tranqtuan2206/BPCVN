using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaceToSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Space",
                table: "Specs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Space",
                table: "Specs");
        }
    }
}
