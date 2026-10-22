using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEmptyUnitWeightDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Transactions] SET [EmptyUnitWeight] = 8.01 WHERE [EmptyUnitWeight] = 800");

            migrationBuilder.AlterColumn<decimal>(
                name: "EmptyUnitWeight",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 8.01m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 800m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Transactions] SET [EmptyUnitWeight] = 800 WHERE [EmptyUnitWeight] = 8.01");

            migrationBuilder.AlterColumn<decimal>(
                name: "EmptyUnitWeight",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 800m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 8.01m);
        }
    }
}
