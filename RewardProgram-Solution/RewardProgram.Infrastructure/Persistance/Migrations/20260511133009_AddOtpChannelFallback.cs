using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewardProgram.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpChannelFallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "OtpCodes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "whatsapp");

            migrationBuilder.AddColumn<string>(
                name: "CurrentSid",
                table: "OtpCodes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "FallbackFired",
                table: "OtpCodes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_CurrentSid",
                table: "OtpCodes",
                column: "CurrentSid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_CurrentSid",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "CurrentSid",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "FallbackFired",
                table: "OtpCodes");
        }
    }
}
