using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Statement = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Incoming = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Outgoing = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FarmMealBatchId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseEntries_FarmMealBatches_FarmMealBatchId",
                        column: x => x.FarmMealBatchId,
                        principalTable: "FarmMealBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_FarmMealBatchId",
                table: "ExpenseEntries",
                column: "FarmMealBatchId",
                unique: true,
                filter: "[FarmMealBatchId] IS NOT NULL");

            migrationBuilder.Sql(
                """
                INSERT INTO [ExpenseEntries] ([Statement], [Incoming], [Outgoing], [Date], [FarmMealBatchId])
                SELECT
                    CASE WHEN profit.Amount > 0
                        THEN N'أرباح وجبات - ' + batch.[FarmName]
                        ELSE N'خسارة وجبات - ' + batch.[FarmName]
                    END,
                    CASE WHEN profit.Amount > 0 THEN profit.Amount ELSE 0 END,
                    CASE WHEN profit.Amount < 0 THEN -profit.Amount ELSE 0 END,
                    batch.[Date],
                    batch.[Id]
                FROM [FarmMealBatches] AS batch
                CROSS APPLY (
                    SELECT
                        (batch.[FarmWeight] - batch.[NetWeight]) * batch.[FarmPrice]
                        + batch.[MealTotal]
                        - batch.[FarmPrice] * batch.[FarmWeight] AS Amount
                ) AS profit
                WHERE profit.Amount <> 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseEntries");
        }
    }
}
