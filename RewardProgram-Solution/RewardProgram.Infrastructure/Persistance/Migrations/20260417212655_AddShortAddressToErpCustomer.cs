using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddShortAddressToErpCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortAddress",
                table: "ErpCustomers",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_ShortAddress",
                table: "ErpCustomers",
                column: "ShortAddress",
                unique: true,
                filter: "[IsDeleted] = 0 AND [ShortAddress] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ErpCustomers_ShortAddress",
                table: "ErpCustomers");

            migrationBuilder.DropColumn(
                name: "ShortAddress",
                table: "ErpCustomers");
        }
    }
}
