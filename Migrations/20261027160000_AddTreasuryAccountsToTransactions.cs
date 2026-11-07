using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryAccountsToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CashTreasuryAccount",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ElectronicTreasuryAccount",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransactionId",
                table: "TreasuryEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryEntries_TransactionId",
                table: "TreasuryEntries",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryEntries_Transactions_TransactionId",
                table: "TreasuryEntries",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryEntries_Transactions_TransactionId",
                table: "TreasuryEntries");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryEntries_TransactionId",
                table: "TreasuryEntries");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "TreasuryEntries");

            migrationBuilder.DropColumn(
                name: "CashTreasuryAccount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ElectronicTreasuryAccount",
                table: "Transactions");
        }
    }
}
