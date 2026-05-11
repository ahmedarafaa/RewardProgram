using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizationKeysToNotificationsAndWalletTxs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionArgs",
                table: "WalletTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionKey",
                table: "WalletTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyArgs",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyKey",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleKey",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionArgs",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "DescriptionKey",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "BodyArgs",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "BodyKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TitleKey",
                table: "Notifications");
        }
    }
}
