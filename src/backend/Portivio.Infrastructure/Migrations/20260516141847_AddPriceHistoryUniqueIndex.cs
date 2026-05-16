using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portivio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceHistoryUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete duplicates before creating the unique index
            // Using ROW_NUMBER() because MIN(uuid) is not supported in Postgres
            migrationBuilder.Sql(@"
                DELETE FROM ""PriceHistories""
                WHERE ""Id"" IN (
                    SELECT ""Id""
                    FROM (
                        SELECT ""Id"",
                               ROW_NUMBER() OVER (
                                   PARTITION BY ""InstrumentId"", ""Date""
                                   ORDER BY ""CreatedAt"" DESC, ""Id""
                               ) AS rnk
                        FROM ""PriceHistories""
                    ) t
                    WHERE rnk > 1
                );
            ");

            migrationBuilder.DropIndex(
                name: "idx_pricehistory_instrument_date",
                table: "PriceHistories");

            migrationBuilder.CreateIndex(
                name: "idx_pricehistory_instrument_date_unique",
                table: "PriceHistories",
                columns: new[] { "InstrumentId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_pricehistory_instrument_date_unique",
                table: "PriceHistories");

            migrationBuilder.CreateIndex(
                name: "idx_pricehistory_instrument_date",
                table: "PriceHistories",
                columns: new[] { "InstrumentId", "Date" });
        }
    }
}
