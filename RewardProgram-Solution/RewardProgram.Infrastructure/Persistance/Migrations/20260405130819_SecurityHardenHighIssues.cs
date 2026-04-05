using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardenHighIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_CustomerCode",
                table: "ShopOwnerProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_CustomerCode",
                table: "ShopOwnerProfiles",
                column: "CustomerCode",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_CustomerCode",
                table: "ShopOwnerProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_CustomerCode",
                table: "ShopOwnerProfiles",
                column: "CustomerCode");
        }
    }
}
