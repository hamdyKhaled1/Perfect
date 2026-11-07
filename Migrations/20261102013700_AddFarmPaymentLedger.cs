using HagImamAccounts.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261102013700_AddFarmPaymentLedger")]
    public partial class AddFarmPaymentLedger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FarmPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FarmName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmPayments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmPayments_FarmName_Date_Id",
                table: "FarmPayments",
                columns: new[] { "FarmName", "Date", "Id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FarmPayments");
        }
    }
}
