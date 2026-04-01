using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardenCashOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashOtp",
                table: "RedemptionRequests");

            migrationBuilder.AddColumn<int>(
                name: "CashOtpAttempts",
                table: "RedemptionRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CashOtpHash",
                table: "RedemptionRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashOtpAttempts",
                table: "RedemptionRequests");

            migrationBuilder.DropColumn(
                name: "CashOtpHash",
                table: "RedemptionRequests");

            migrationBuilder.AddColumn<string>(
                name: "CashOtp",
                table: "RedemptionRequests",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);
        }
    }
}
