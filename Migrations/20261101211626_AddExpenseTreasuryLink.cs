using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseTreasuryLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TreasuryEntryId",
                table: "ExpenseEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_TreasuryEntryId",
                table: "ExpenseEntries",
                column: "TreasuryEntryId",
                unique: true,
                filter: "[TreasuryEntryId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseEntries_TreasuryEntries_TreasuryEntryId",
                table: "ExpenseEntries",
                column: "TreasuryEntryId",
                principalTable: "TreasuryEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseEntries_TreasuryEntries_TreasuryEntryId",
                table: "ExpenseEntries");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseEntries_TreasuryEntryId",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "TreasuryEntryId",
                table: "ExpenseEntries");
        }
    }
}
