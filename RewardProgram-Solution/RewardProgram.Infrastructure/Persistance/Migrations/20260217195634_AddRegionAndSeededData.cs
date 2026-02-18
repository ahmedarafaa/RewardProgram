using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionAndSeededData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "FK_Districts_AspNetUsers_ApprovalSalesManId",
                table: "Districts");

            migrationBuilder.DropIndex(
                name: "IX_Districts_ApprovalSalesManId",
                table: "Districts");

            migrationBuilder.DropIndex(
                name: "IX_Districts_Zone",
                table: "Districts");

            migrationBuilder.DropIndex(
                name: "IX_Cities_NameAr",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Cities_NameEn",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DistrictId",
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
                name: "IX_AspNetUsers_NationalAddress_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ZoneManagerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApprovalSalesManId",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "Zone",
                table: "Districts");

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
                name: "ZoneManagerId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalSalesManId",
                table: "Cities",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegionId",
                table: "Cities",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ZoneManagerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
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
                    table.PrimaryKey("PK_Regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regions_AspNetUsers_ZoneManagerId",
                        column: x => x.ZoneManagerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cities_ApprovalSalesManId",
                table: "Cities",
                column: "ApprovalSalesManId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_RegionId",
                table: "Cities",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_RegionId_NameAr",
                table: "Cities",
                columns: new[] { "RegionId", "NameAr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_RegionId_NameEn",
                table: "Cities",
                columns: new[] { "RegionId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_IsActive",
                table: "Regions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_NameAr",
                table: "Regions",
                column: "NameAr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_NameEn",
                table: "Regions",
                column: "NameEn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_ZoneManagerId",
                table: "Regions",
                column: "ZoneManagerId",
                unique: true,
                filter: "[ZoneManagerId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Cities_AspNetUsers_ApprovalSalesManId",
                table: "Cities",
                column: "ApprovalSalesManId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Cities_Regions_RegionId",
                table: "Cities",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cities_AspNetUsers_ApprovalSalesManId",
                table: "Cities");

            migrationBuilder.DropForeignKey(
                name: "FK_Cities_Regions_RegionId",
                table: "Cities");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_Cities_ApprovalSalesManId",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Cities_RegionId",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Cities_RegionId_NameAr",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Cities_RegionId_NameEn",
                table: "Cities");

            migrationBuilder.DropColumn(
                name: "ApprovalSalesManId",
                table: "Cities");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Cities");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalSalesManId",
                table: "Districts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Zone",
                table: "Districts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DistrictId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagedCityId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagedZone",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZoneManagerId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ApprovalSalesManId",
                table: "Districts",
                column: "ApprovalSalesManId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_Zone",
                table: "Districts",
                column: "Zone");

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
                name: "IX_AspNetUsers_DistrictId",
                table: "AspNetUsers",
                column: "DistrictId");

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
                name: "IX_AspNetUsers_NationalAddress_DistrictId",
                table: "AspNetUsers",
                column: "NationalAddress_DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ZoneManagerId",
                table: "AspNetUsers",
                column: "ZoneManagerId");

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
                name: "FK_Districts_AspNetUsers_ApprovalSalesManId",
                table: "Districts",
                column: "ApprovalSalesManId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
