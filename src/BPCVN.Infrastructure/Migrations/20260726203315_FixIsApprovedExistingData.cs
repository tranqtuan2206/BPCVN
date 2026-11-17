using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixIsApprovedExistingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix data cÅ© bá»‹ set IsApproved=0 do migration trÆ°á»›c dÃ¹ng defaultValue: false.
            // Táº¥t cáº£ Kit/Switch/Keycap Ä‘Æ°á»£c táº¡o TRÆ¯á»šC há»‡ thá»‘ng Pending Queue Ä‘á»u pháº£i IsApproved=1.
            migrationBuilder.Sql("UPDATE Kits     SET IsApproved = 1 WHERE IsApproved = 0");
            migrationBuilder.Sql("UPDATE Switches  SET IsApproved = 1 WHERE IsApproved = 0");
            migrationBuilder.Sql("UPDATE Keycaps   SET IsApproved = 1 WHERE IsApproved = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // KhÃ´ng rollback â€” khÃ´ng cÃ³ cÃ¡ch phÃ¢n biá»‡t data cÅ© vs data pending sau khi down
        }
    }
}
