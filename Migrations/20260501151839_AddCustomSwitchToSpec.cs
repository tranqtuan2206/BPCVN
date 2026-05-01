using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomSwitchToSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Specs_Switches_SwitchId",
                table: "Specs");

            migrationBuilder.AlterColumn<int>(
                name: "SwitchId",
                table: "Specs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CustomSwitchName",
                table: "Specs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Specs_Switches_SwitchId",
                table: "Specs",
                column: "SwitchId",
                principalTable: "Switches",
                principalColumn: "SwitchId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Specs_Switches_SwitchId",
                table: "Specs");

            migrationBuilder.DropColumn(
                name: "CustomSwitchName",
                table: "Specs");

            migrationBuilder.AlterColumn<int>(
                name: "SwitchId",
                table: "Specs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Specs_Switches_SwitchId",
                table: "Specs",
                column: "SwitchId",
                principalTable: "Switches",
                principalColumn: "SwitchId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
