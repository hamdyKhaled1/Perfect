using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddClientFarmPriceIncrements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultPanehPriceIncrement",
                table: "Clients",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultPriceIncrement",
                table: "Clients",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DefaultPanehPriceIncrement", "DefaultPriceIncrement" },
                values: new object[] { 0m, 0m });

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DefaultPanehPriceIncrement", "DefaultPriceIncrement" },
                values: new object[] { 0m, 0m });

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DefaultPanehPriceIncrement", "DefaultPriceIncrement" },
                values: new object[] { 0m, 0m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultPanehPriceIncrement",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DefaultPriceIncrement",
                table: "Clients");
        }
    }
}
