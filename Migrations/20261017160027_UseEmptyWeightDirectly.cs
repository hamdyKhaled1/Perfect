using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class UseEmptyWeightDirectly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EmptyCount",
                table: "Transactions",
                newName: "EmptyWeight");

            migrationBuilder.AlterColumn<decimal>(
                name: "EmptyWeight",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.DropColumn(
                name: "EmptyUnitWeight",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EmptyUnitWeight",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 8.01m);

            migrationBuilder.AlterColumn<int>(
                name: "EmptyWeight",
                table: "Transactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.RenameColumn(
                name: "EmptyWeight",
                table: "Transactions",
                newName: "EmptyCount");
        }
    }
}
