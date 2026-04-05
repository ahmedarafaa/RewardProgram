using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class MediumIssuesFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopData_CRN",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ShopData_CustomerCode",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ShopData_ShortAddress",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ShopData_VAT",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ScanRecords_BarcodeId_ScannerRole",
                table: "ScanRecords");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductCode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBarcodes_Code",
                table: "ProductBarcodes");

            migrationBuilder.DropIndex(
                name: "IX_ErpCustomers_CustomerCode",
                table: "ErpCustomers");

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_CRN",
                table: "ShopData",
                column: "CRN",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_CustomerCode",
                table: "ShopData",
                column: "CustomerCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_ShortAddress",
                table: "ShopData",
                column: "ShortAddress",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_VAT",
                table: "ShopData",
                column: "VAT",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ScanRecords_BarcodeId_ScannerRole",
                table: "ScanRecords",
                columns: new[] { "BarcodeId", "ScannerRole" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCode",
                table: "Products",
                column: "ProductCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_Code",
                table: "ProductBarcodes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_CustomerCode",
                table: "ErpCustomers",
                column: "CustomerCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AspNetUsers_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AspNetUsers_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_ShopData_CRN",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ShopData_CustomerCode",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ShopData_ShortAddress",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ShopData_VAT",
                table: "ShopData");

            migrationBuilder.DropIndex(
                name: "IX_ScanRecords_BarcodeId_ScannerRole",
                table: "ScanRecords");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductCode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBarcodes_Code",
                table: "ProductBarcodes");

            migrationBuilder.DropIndex(
                name: "IX_ErpCustomers_CustomerCode",
                table: "ErpCustomers");

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_CRN",
                table: "ShopData",
                column: "CRN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_CustomerCode",
                table: "ShopData",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_ShortAddress",
                table: "ShopData",
                column: "ShortAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_VAT",
                table: "ShopData",
                column: "VAT",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanRecords_BarcodeId_ScannerRole",
                table: "ScanRecords",
                columns: new[] { "BarcodeId", "ScannerRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCode",
                table: "Products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_Code",
                table: "ProductBarcodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_CustomerCode",
                table: "ErpCustomers",
                column: "CustomerCode",
                unique: true);
        }
    }
}
