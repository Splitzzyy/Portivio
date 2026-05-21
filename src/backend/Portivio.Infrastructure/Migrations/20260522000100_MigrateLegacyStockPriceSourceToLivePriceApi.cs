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
            migrationBuilder.Sql(@"
                UPDATE ""Instruments""
                SET ""PriceSource"" = 1
                WHERE ""PriceSource"" = 5
                  AND ""Category"" = 0;
            ");
        }
    }
}
