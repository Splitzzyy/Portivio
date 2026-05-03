using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portivio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionIngestFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientTxnId",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "ux_transactions_profile_clienttxnid",
                table: "Transactions",
                columns: new[] { "ProfileId", "ClientTxnId" },
                unique: true,
                filter: "\"ClientTxnId\" IS NOT NULL");

            // Backfill existing rows: Source=Manual(0), timestamps from TransactionDate
            migrationBuilder.Sql("""
                UPDATE "Transactions"
                SET "Source" = 0,
                    "CreatedAtUtc" = "TransactionDate",
                    "UpdatedAtUtc" = "TransactionDate";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_transactions_profile_clienttxnid",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ClientTxnId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Transactions");
        }
    }
}
