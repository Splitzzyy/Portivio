using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portivio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentAssetTypeSymbolUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_instruments_assettype_symbol",
                table: "Instruments",
                columns: new[] { "AssetTypeId", "Symbol" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_instruments_assettype_symbol",
                table: "Instruments");
        }
    }
}
