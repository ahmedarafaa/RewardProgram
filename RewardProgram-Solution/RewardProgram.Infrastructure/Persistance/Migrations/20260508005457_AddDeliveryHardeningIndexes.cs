using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryHardeningIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_InvitationReward_Unique",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "ReferenceId" },
                unique: true,
                filter: "[Type] = 5 AND [ReferenceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsDeleted_CreatedAt",
                table: "Notifications",
                columns: new[] { "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CreatedAt_Desc",
                table: "AspNetUsers",
                column: "CreatedAt",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_InvitationReward_Unique",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsDeleted_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CreatedAt_Desc",
                table: "AspNetUsers");
        }
    }
}
