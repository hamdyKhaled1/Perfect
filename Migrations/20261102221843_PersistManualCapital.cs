using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class PersistManualCapital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapitalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ManualCapital = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClientBalances = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OutsideCustomerBalances = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TreasuryBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NetExpensesWithoutTreasury = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FarmAccount = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapitalSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapitalSettings");
        }
    }
}
