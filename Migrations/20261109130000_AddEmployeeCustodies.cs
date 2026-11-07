using HagImamAccounts.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261109130000_AddEmployeeCustodies")]
    public partial class AddEmployeeCustodies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCustodies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TreasuryAccount = table.Column<int>(type: "int", nullable: false),
                    TreasuryEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCustodies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCustodies_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeCustodies_TreasuryEntries_TreasuryEntryId",
                        column: x => x.TreasuryEntryId,
                        principalTable: "TreasuryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeCustodyMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeCustodyId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCustodyMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCustodyMovements_EmployeeCustodies_EmployeeCustodyId",
                        column: x => x.EmployeeCustodyId,
                        principalTable: "EmployeeCustodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCustodies_EmployeeId_Date_Id",
                table: "EmployeeCustodies",
                columns: new[] { "EmployeeId", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCustodies_TreasuryEntryId",
                table: "EmployeeCustodies",
                column: "TreasuryEntryId",
                unique: true,
                filter: "[TreasuryEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCustodyMovements_EmployeeCustodyId_Date_Id",
                table: "EmployeeCustodyMovements",
                columns: new[] { "EmployeeCustodyId", "Date", "Id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmployeeCustodyMovements");
            migrationBuilder.DropTable(name: "EmployeeCustodies");
        }
    }
}
