using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portivio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyStockPriceSourceToLivePriceApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing stock instruments with the removed legacy provider enum
            // value now use the LivePriceApi provider.
            migrationBuilder.Sql(@"
                UPDATE ""Instruments""
                SET ""PriceSource"" = 5
                WHERE ""PriceSource"" = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No safe rollback: once old rows are normalized to LivePriceApi, they
            // cannot be distinguished from instruments that were already using it.
            // Reintroducing the removed enum value would also strand those rows.
        }
    }
}
