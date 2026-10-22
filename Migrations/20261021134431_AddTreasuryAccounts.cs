using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreasuryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Account = table.Column<int>(type: "int", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Incoming = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Outgoing = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryEntries_Account_Date_Id",
                table: "TreasuryEntries",
                columns: new[] { "Account", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryEntries_TransferId",
                table: "TreasuryEntries",
                column: "TransferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreasuryEntries");
        }
    }
}
