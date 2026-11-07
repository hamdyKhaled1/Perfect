using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmAdditionAndDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FarmAdditionAmount",
                table: "FarmMealBatches",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FarmDiscountAmount",
                table: "FarmMealBatches",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmAdditionAmount",
                table: "FarmMealBatches");

            migrationBuilder.DropColumn(
                name: "FarmDiscountAmount",
                table: "FarmMealBatches");
        }
    }
}
