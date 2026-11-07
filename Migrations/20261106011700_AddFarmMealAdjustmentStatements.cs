using HagImamAccounts.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261106011700_AddFarmMealAdjustmentStatements")]
    public partial class AddFarmMealAdjustmentStatements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FarmAdditionStatement",
                table: "FarmMealBatches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmDiscountStatement",
                table: "FarmMealBatches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmAdditionStatement",
                table: "FarmMealBatches");

            migrationBuilder.DropColumn(
                name: "FarmDiscountStatement",
                table: "FarmMealBatches");
        }
    }
}
