using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portivio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentCategoryPriceSourceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Instruments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Isin",
                table: "Instruments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Metadata",
                table: "Instruments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceSource",
                table: "Instruments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PriceSourceKey",
                table: "Instruments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_isin",
                table: "Instruments",
                column: "Isin");

            // Backfill Category from AssetType.Name — maps common names, defaults to 0 (Equity)
            migrationBuilder.Sql("""
                UPDATE "Instruments" i
                SET "Category" = CASE LOWER(at."Name")
                    WHEN 'equity'            THEN 0
                    WHEN 'stock'             THEN 0
                    WHEN 'stocks'            THEN 0
                    WHEN 'mutual fund'       THEN 1
                    WHEN 'mutualfund'        THEN 1
                    WHEN 'mf'                THEN 1
                    WHEN 'fixed deposit'     THEN 2
                    WHEN 'fixeddeposit'      THEN 2
                    WHEN 'fd'                THEN 2
                    WHEN 'recurring deposit' THEN 3
                    WHEN 'recurringdeposit'  THEN 3
                    WHEN 'rd'                THEN 3
                    WHEN 'ppf'               THEN 4
                    WHEN 'epf'               THEN 5
                    WHEN 'gold'              THEN 6
                    WHEN 'bond'              THEN 7
                    WHEN 'bonds'             THEN 7
                    WHEN 'crypto'            THEN 8
                    WHEN 'cryptocurrency'    THEN 8
                    WHEN 'real estate'       THEN 9
                    WHEN 'realestate'        THEN 9
                    WHEN 'cash'              THEN 10
                    ELSE 0
                END
                FROM "AssetTypes" at
                WHERE i."AssetTypeId" = at."Id";
                """);

            // GIN index for jsonb metadata filtering — Postgres-only, no EF fluent API equivalent
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "ix_instruments_metadata_gin" ON "Instruments" USING gin ("Metadata");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "ix_instruments_metadata_gin";""");

            migrationBuilder.DropIndex(
                name: "ix_instruments_isin",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "Isin",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "PriceSource",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "PriceSourceKey",
                table: "Instruments");
        }
    }
}
