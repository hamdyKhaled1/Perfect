using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmMealBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FarmMealBatchId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FarmMealBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FarmName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FarmWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FarmPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MealTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmMealBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FarmMealBatchId",
                table: "Transactions",
                column: "FarmMealBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_FarmMealBatches_FarmMealBatchId",
                table: "Transactions",
                column: "FarmMealBatchId",
                principalTable: "FarmMealBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_FarmMealBatches_FarmMealBatchId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "FarmMealBatches");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_FarmMealBatchId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FarmMealBatchId",
                table: "Transactions");
        }
    }
}
