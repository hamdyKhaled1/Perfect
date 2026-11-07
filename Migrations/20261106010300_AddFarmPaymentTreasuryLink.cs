using HagImamAccounts.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261106010300_AddFarmPaymentTreasuryLink")]
    public partial class AddFarmPaymentTreasuryLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TreasuryEntryId",
                table: "FarmPayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmPayments_TreasuryEntryId",
                table: "FarmPayments",
                column: "TreasuryEntryId",
                unique: true,
                filter: "[TreasuryEntryId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FarmPayments_TreasuryEntries_TreasuryEntryId",
                table: "FarmPayments",
                column: "TreasuryEntryId",
                principalTable: "TreasuryEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FarmPayments_TreasuryEntries_TreasuryEntryId",
                table: "FarmPayments");

            migrationBuilder.DropIndex(
                name: "IX_FarmPayments_TreasuryEntryId",
                table: "FarmPayments");

            migrationBuilder.DropColumn(
                name: "TreasuryEntryId",
                table: "FarmPayments");
        }
    }
}
