using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class ShopDataErpCustomerSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SellerProfiles_ShopOwnerProfiles_ShopOwnerId",
                table: "SellerProfiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ShopOwnerProfiles_UserId",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_CRN",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_ShopCode",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_StoreName",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_VAT",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SellerProfiles_ShopOwnerId",
                table: "SellerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SellerProfiles_ShopOwnerId_CreatedAt",
                table: "SellerProfiles");

            migrationBuilder.DropColumn(
                name: "CRN",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropColumn(
                name: "ShopCode",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropColumn(
                name: "ShopImageUrl",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropColumn(
                name: "StoreName",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropColumn(
                name: "VAT",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropColumn(
                name: "ShopOwnerId",
                table: "SellerProfiles");

            migrationBuilder.DropColumn(
                name: "NationalAddress_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "CustomerCode",
                table: "ShopOwnerProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerCode",
                table: "SellerProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ErpCustomers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpCustomers", x => x.Id);
                    table.UniqueConstraint("AK_ErpCustomers_CustomerCode", x => x.CustomerCode);
                });

            migrationBuilder.CreateTable(
                name: "ShopData",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VAT = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CRN = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ShopImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Street = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BuildingNumber = table.Column<int>(type: "int", nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    SubNumber = table.Column<int>(type: "int", nullable: false),
                    EnteredByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopData_AspNetUsers_EnteredByUserId",
                        column: x => x.EnteredByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopData_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopData_ErpCustomers_CustomerCode",
                        column: x => x.CustomerCode,
                        principalTable: "ErpCustomers",
                        principalColumn: "CustomerCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_CustomerCode",
                table: "ShopOwnerProfiles",
                column: "CustomerCode");

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_CustomerCode",
                table: "SellerProfiles",
                column: "CustomerCode");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_CustomerCode",
                table: "ErpCustomers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_CityId",
                table: "ShopData",
                column: "CityId");

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
                name: "IX_ShopData_EnteredByUserId",
                table: "ShopData",
                column: "EnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_StoreName",
                table: "ShopData",
                column: "StoreName");

            migrationBuilder.CreateIndex(
                name: "IX_ShopData_VAT",
                table: "ShopData",
                column: "VAT",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerProfiles_ErpCustomers_CustomerCode",
                table: "SellerProfiles",
                column: "CustomerCode",
                principalTable: "ErpCustomers",
                principalColumn: "CustomerCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShopOwnerProfiles_ErpCustomers_CustomerCode",
                table: "ShopOwnerProfiles",
                column: "CustomerCode",
                principalTable: "ErpCustomers",
                principalColumn: "CustomerCode",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SellerProfiles_ErpCustomers_CustomerCode",
                table: "SellerProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ShopOwnerProfiles_ErpCustomers_CustomerCode",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropTable(
                name: "ShopData");

            migrationBuilder.DropTable(
                name: "ErpCustomers");

            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_CustomerCode",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SellerProfiles_CustomerCode",
                table: "SellerProfiles");

            migrationBuilder.DropColumn(
                name: "CustomerCode",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropColumn(
                name: "CustomerCode",
                table: "SellerProfiles");

            migrationBuilder.AddColumn<string>(
                name: "CRN",
                table: "ShopOwnerProfiles",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShopCode",
                table: "ShopOwnerProfiles",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopImageUrl",
                table: "ShopOwnerProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                table: "ShopOwnerProfiles",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VAT",
                table: "ShopOwnerProfiles",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShopOwnerId",
                table: "SellerProfiles",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NationalAddress_DistrictId",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ShopOwnerProfiles_UserId",
                table: "ShopOwnerProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_CRN",
                table: "ShopOwnerProfiles",
                column: "CRN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_ShopCode",
                table: "ShopOwnerProfiles",
                column: "ShopCode",
                unique: true,
                filter: "[ShopCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_StoreName",
                table: "ShopOwnerProfiles",
                column: "StoreName");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_VAT",
                table: "ShopOwnerProfiles",
                column: "VAT",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_ShopOwnerId",
                table: "SellerProfiles",
                column: "ShopOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_ShopOwnerId_CreatedAt",
                table: "SellerProfiles",
                columns: new[] { "ShopOwnerId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SellerProfiles_ShopOwnerProfiles_ShopOwnerId",
                table: "SellerProfiles",
                column: "ShopOwnerId",
                principalTable: "ShopOwnerProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
