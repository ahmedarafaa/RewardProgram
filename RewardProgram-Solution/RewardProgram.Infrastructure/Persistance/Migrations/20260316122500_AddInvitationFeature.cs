using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InviteeRewardPoints",
                table: "RewardSettings",
                type: "decimal(10,1)",
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<decimal>(
                name: "InviterRewardPoints",
                table: "RewardSettings",
                type: "decimal(10,1)",
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<string>(
                name: "InvitationCode",
                table: "AspNetUsers",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitedByUserId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_InvitationCode",
                table: "AspNetUsers",
                column: "InvitationCode",
                unique: true,
                filter: "[InvitationCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_InvitedByUserId",
                table: "AspNetUsers",
                column: "InvitedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_InvitedByUserId",
                table: "AspNetUsers",
                column: "InvitedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_InvitedByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_InvitationCode",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_InvitedByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "InviteeRewardPoints",
                table: "RewardSettings");

            migrationBuilder.DropColumn(
                name: "InviterRewardPoints",
                table: "RewardSettings");

            migrationBuilder.DropColumn(
                name: "InvitationCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "AspNetUsers");
        }
    }
}
