using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portivio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldingPnLSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AccruedInterest",
                table: "Holdings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RealizedPnL",
                table: "Holdings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Snapshot",
                table: "Holdings",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccruedInterest",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "RealizedPnL",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "Snapshot",
                table: "Holdings");
        }
    }
}
