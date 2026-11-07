using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayrollEntryId",
                table: "ExpenseEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AbsenceDays = table.Column<int>(type: "int", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Bonus = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AdvancesDeduction = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAdvances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayrollEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAdvances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAdvances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeAdvances_PayrollEntries_PayrollEntryId",
                        column: x => x.PayrollEntryId,
                        principalTable: "PayrollEntries",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_PayrollEntryId",
                table: "ExpenseEntries",
                column: "PayrollEntryId",
                unique: true,
                filter: "[PayrollEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAdvances_EmployeeId_Date",
                table: "EmployeeAdvances",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAdvances_PayrollEntryId",
                table: "EmployeeAdvances",
                column: "PayrollEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Name",
                table: "Employees",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_EmployeeId_Year_Month",
                table: "PayrollEntries",
                columns: new[] { "EmployeeId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_Year_Month",
                table: "PayrollEntries",
                columns: new[] { "Year", "Month" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseEntries_PayrollEntries_PayrollEntryId",
                table: "ExpenseEntries",
                column: "PayrollEntryId",
                principalTable: "PayrollEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseEntries_PayrollEntries_PayrollEntryId",
                table: "ExpenseEntries");

            migrationBuilder.DropTable(
                name: "EmployeeAdvances");

            migrationBuilder.DropTable(
                name: "PayrollEntries");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseEntries_PayrollEntryId",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "PayrollEntryId",
                table: "ExpenseEntries");
        }
    }
}
