using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class EditOtpCodeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CitySalesManMappings");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_Code",
                table: "OtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_MobileNumber_Code_Purpose",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OtpCodes");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "OtpCodes",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PinId",
                table: "OtpCodes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_IsUsed",
                table: "OtpCodes",
                column: "IsUsed");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_PinId",
                table: "OtpCodes",
                column: "PinId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_IsUsed",
                table: "OtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_PinId",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "PinId",
                table: "OtpCodes");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "OtpCodes",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "OtpCodes",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "OtpCodes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "OtpCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CitySalesManMappings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SalesManId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CitySalesManMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CitySalesManMappings_AspNetUsers_SalesManId",
                        column: x => x.SalesManId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_Code",
                table: "OtpCodes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_MobileNumber_Code_Purpose",
                table: "OtpCodes",
                columns: new[] { "MobileNumber", "Code", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_CitySalesManMappings_City",
                table: "CitySalesManMappings",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_CitySalesManMappings_City_IsActive",
                table: "CitySalesManMappings",
                columns: new[] { "City", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CitySalesManMappings_SalesManId",
                table: "CitySalesManMappings",
                column: "SalesManId");
        }
    }
}
