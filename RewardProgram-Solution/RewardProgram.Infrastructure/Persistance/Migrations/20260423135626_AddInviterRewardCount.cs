using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddInviterRewardCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InviterRewardCount",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill for existing inviters: count the InvitationReward transactions
            // (Type = 6) on their wallet that reference an invitee (ReferenceId != their
            // own Id). Without this, previously-rewarded inviters would start fresh at 0
            // and could exceed the 20-reward cap.
            migrationBuilder.Sql(@"
                UPDATE u
                SET u.InviterRewardCount = ISNULL(x.RewardCount, 0)
                FROM AspNetUsers u
                OUTER APPLY (
                    SELECT COUNT_BIG(*) AS RewardCount
                    FROM WalletTransactions wt
                    INNER JOIN Wallets w ON w.Id = wt.WalletId
                    WHERE w.UserId = u.Id
                      AND wt.Type = 6
                      AND wt.ReferenceId <> u.Id
                      AND wt.IsDeleted = 0
                ) x;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InviterRewardCount",
                table: "AspNetUsers");
        }
    }
}
