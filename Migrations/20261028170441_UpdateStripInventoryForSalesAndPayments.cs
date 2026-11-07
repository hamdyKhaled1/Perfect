using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStripInventoryForSalesAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "StripInventoryEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Cartons",
                table: "StripInventoryEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EntryType",
                table: "StripInventoryEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "StripInventoryEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerCarton",
                table: "StripInventoryEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "StripInventoryEntries");

            migrationBuilder.DropColumn(
                name: "Cartons",
                table: "StripInventoryEntries");

            migrationBuilder.DropColumn(
                name: "EntryType",
                table: "StripInventoryEntries");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "StripInventoryEntries");

            migrationBuilder.DropColumn(
                name: "PricePerCarton",
                table: "StripInventoryEntries");
        }
    }
}
