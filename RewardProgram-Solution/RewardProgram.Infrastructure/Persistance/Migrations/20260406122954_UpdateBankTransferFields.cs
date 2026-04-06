using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBankTransferFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BankName",
                table: "RedemptionRequests",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "AccountHolderName",
                table: "RedemptionRequests",
                newName: "AccountName");

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "RedemptionRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwiftCode",
                table: "RedemptionRequests",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "RedemptionRequests");

            migrationBuilder.DropColumn(
                name: "SwiftCode",
                table: "RedemptionRequests");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "RedemptionRequests",
                newName: "BankName");

            migrationBuilder.RenameColumn(
                name: "AccountName",
                table: "RedemptionRequests",
                newName: "AccountHolderName");
        }
    }
}
