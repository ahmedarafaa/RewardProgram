using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddRedemptionFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAmount",
                table: "WalletTransactions",
                type: "decimal(8,1)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeldBalance",
                table: "Wallets",
                type: "decimal(10,1)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeldSarBalance",
                table: "Wallets",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill: set RemainingAmount = Amount for all existing Earned transactions
            migrationBuilder.Sql("UPDATE WalletTransactions SET RemainingAmount = Amount WHERE Type = 1");

            migrationBuilder.CreateTable(
                name: "RedemptionRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PointsAmount = table.Column<decimal>(type: "decimal(10,1)", nullable: false),
                    SarRate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SarAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccountHolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CashOtp = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    CashOtpExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CashHandoverById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CashHandoverAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
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
                    table.PrimaryKey("PK_RedemptionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RedemptionRequests_AspNetUsers_CashHandoverById",
                        column: x => x.CashHandoverById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedemptionRequests_AspNetUsers_RejectedById",
                        column: x => x.RejectedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedemptionRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RedemptionApprovals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RedemptionRequestId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApproverId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RedemptionApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RedemptionApprovals_AspNetUsers_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedemptionApprovals_RedemptionRequests_RedemptionRequestId",
                        column: x => x.RedemptionRequestId,
                        principalTable: "RedemptionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionApprovals_ApproverId",
                table: "RedemptionApprovals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionApprovals_RedemptionRequestId",
                table: "RedemptionApprovals",
                column: "RedemptionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRequests_CashHandoverById",
                table: "RedemptionRequests",
                column: "CashHandoverById");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRequests_RejectedById",
                table: "RedemptionRequests",
                column: "RejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRequests_Status",
                table: "RedemptionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRequests_UserId",
                table: "RedemptionRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedemptionApprovals");

            migrationBuilder.DropTable(
                name: "RedemptionRequests");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "HeldBalance",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "HeldSarBalance",
                table: "Wallets");
        }
    }
}
