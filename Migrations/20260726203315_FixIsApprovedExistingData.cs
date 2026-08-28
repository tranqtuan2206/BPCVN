using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Migrations
{
    /// <inheritdoc />
    public partial class FixIsApprovedExistingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix data cũ bị set IsApproved=0 do migration trước dùng defaultValue: false.
            // Tất cả Kit/Switch/Keycap được tạo TRƯỚC hệ thống Pending Queue đều phải IsApproved=1.
            migrationBuilder.Sql("UPDATE Kits     SET IsApproved = 1 WHERE IsApproved = 0");
            migrationBuilder.Sql("UPDATE Switches  SET IsApproved = 1 WHERE IsApproved = 0");
            migrationBuilder.Sql("UPDATE Keycaps   SET IsApproved = 1 WHERE IsApproved = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không rollback — không có cách phân biệt data cũ vs data pending sau khi down
        }
    }
}
