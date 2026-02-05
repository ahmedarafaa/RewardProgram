using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class FixRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_DistrictManagerId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_SellerProfiles_ShopOwnerProfiles_ShopOwnerId",
                table: "SellerProfiles");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "City",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Neighborhood",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "DistrictManagerId",
                table: "AspNetUsers",
                newName: "ZoneManagerId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUsers_DistrictManagerId",
                table: "AspNetUsers",
                newName: "IX_AspNetUsers_ZoneManagerId");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "OtpCodes",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDisabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DistrictId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagedCityId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagedZone",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NationalAddress_CityId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NationalAddress_DistrictId",
                table: "AspNetUsers",
                type: "int",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ShopOwnerProfiles_UserId",
                table: "ShopOwnerProfiles",
                column: "UserId");

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => new { x.UserId, x.Token });
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Zone = table.Column<int>(type: "int", nullable: false),
                    ApprovalSalesManId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_AspNetUsers_ApprovalSalesManId",
                        column: x => x.ApprovalSalesManId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Districts_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopOwnerProfiles_StoreName",
                table: "ShopOwnerProfiles",
                column: "StoreName");

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_ShopOwnerId_CreatedAt",
                table: "SellerProfiles",
                columns: new[] { "ShopOwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_CreatedAt",
                table: "OtpCodes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_MobileNumber_IsUsed",
                table: "OtpCodes",
                columns: new[] { "MobileNumber", "IsUsed" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_PinId_IsUsed",
                table: "OtpCodes",
                columns: new[] { "PinId", "IsUsed" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DistrictId",
                table: "AspNetUsers",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsDisabled",
                table: "AspNetUsers",
                column: "IsDisabled");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ManagedCityId",
                table: "AspNetUsers",
                column: "ManagedCityId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ManagedCityId_ManagedZone",
                table: "AspNetUsers",
                columns: new[] { "ManagedCityId", "ManagedZone" },
                unique: true,
                filter: "[UserType] = 5 AND [ManagedCityId] IS NOT NULL AND [ManagedZone] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ManagedZone",
                table: "AspNetUsers",
                column: "ManagedZone");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NationalAddress_CityId",
                table: "AspNetUsers",
                column: "NationalAddress_CityId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NationalAddress_DistrictId",
                table: "AspNetUsers",
                column: "NationalAddress_DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserType_IsDisabled",
                table: "AspNetUsers",
                columns: new[] { "UserType", "IsDisabled" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserType_RegistrationStatus",
                table: "AspNetUsers",
                columns: new[] { "UserType", "RegistrationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_Action",
                table: "ApprovalRecords",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_ApproverId_CreatedAt",
                table: "ApprovalRecords",
                columns: new[] { "ApproverId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_CreatedAt",
                table: "ApprovalRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_FromStatus",
                table: "ApprovalRecords",
                column: "FromStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_ToStatus",
                table: "ApprovalRecords",
                column: "ToStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_IsActive",
                table: "Cities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_NameAr",
                table: "Cities",
                column: "NameAr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_NameEn",
                table: "Cities",
                column: "NameEn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ApprovalSalesManId",
                table: "Districts",
                column: "ApprovalSalesManId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_CityId",
                table: "Districts",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_CityId_NameAr",
                table: "Districts",
                columns: new[] { "CityId", "NameAr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_CityId_NameEn",
                table: "Districts",
                columns: new[] { "CityId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_IsActive",
                table: "Districts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_Zone",
                table: "Districts",
                column: "Zone");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresOn",
                table: "RefreshTokens",
                column: "ExpiresOn");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_RevokedOn",
                table: "RefreshTokens",
                column: "RevokedOn");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ZoneManagerId",
                table: "AspNetUsers",
                column: "ZoneManagerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Cities_ManagedCityId",
                table: "AspNetUsers",
                column: "ManagedCityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Districts_DistrictId",
                table: "AspNetUsers",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerProfiles_ShopOwnerProfiles_ShopOwnerId",
                table: "SellerProfiles",
                column: "ShopOwnerId",
                principalTable: "ShopOwnerProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ZoneManagerId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Cities_ManagedCityId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Districts_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_SellerProfiles_ShopOwnerProfiles_ShopOwnerId",
                table: "SellerProfiles");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ShopOwnerProfiles_UserId",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ShopOwnerProfiles_StoreName",
                table: "ShopOwnerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SellerProfiles_ShopOwnerId_CreatedAt",
                table: "SellerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_CreatedAt",
                table: "OtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_MobileNumber_IsUsed",
                table: "OtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_PinId_IsUsed",
                table: "OtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IsDisabled",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ManagedCityId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ManagedCityId_ManagedZone",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ManagedZone",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NationalAddress_CityId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NationalAddress_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserType_IsDisabled",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserType_RegistrationStatus",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_Action",
                table: "ApprovalRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_ApproverId_CreatedAt",
                table: "ApprovalRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_CreatedAt",
                table: "ApprovalRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_FromStatus",
                table: "ApprovalRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_ToStatus",
                table: "ApprovalRecords");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ManagedCityId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ManagedZone",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NationalAddress_CityId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NationalAddress_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "ZoneManagerId",
                table: "AspNetUsers",
                newName: "DistrictManagerId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUsers_ZoneManagerId",
                table: "AspNetUsers",
                newName: "IX_AspNetUsers_DistrictManagerId");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "OtpCodes",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDisabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Neighborhood",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_RefreshToken_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_DistrictManagerId",
                table: "AspNetUsers",
                column: "DistrictManagerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerProfiles_ShopOwnerProfiles_ShopOwnerId",
                table: "SellerProfiles",
                column: "ShopOwnerId",
                principalTable: "ShopOwnerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
