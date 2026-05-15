using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portivio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSummaryPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailSummaryPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: true),
                    TimeOfDay = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    WeeklyDayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    MonthlyDayMode = table.Column<int>(type: "integer", nullable: true),
                    MonthlyDayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastSendStatus = table.Column<int>(type: "integer", nullable: true),
                    LastSendAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSendSucceededAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSendError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    LastManualQueuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSummaryPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailSummaryPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSummaryPreferences_IsEnabled",
                table: "EmailSummaryPreferences",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSummaryPreferences_LockedUntilUtc",
                table: "EmailSummaryPreferences",
                column: "LockedUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSummaryPreferences_NextRunAtUtc",
                table: "EmailSummaryPreferences",
                column: "NextRunAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSummaryPreferences_UserId",
                table: "EmailSummaryPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailSummaryPreferences");
        }
    }
}
