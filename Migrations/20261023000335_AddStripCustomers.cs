using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddStripCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StripCustomerId",
                table: "StripInventoryEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StripCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripCustomers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StripInventoryEntries_StripCustomerId",
                table: "StripInventoryEntries",
                column: "StripCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_StripCustomers_Name",
                table: "StripCustomers",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_StripInventoryEntries_StripCustomers_StripCustomerId",
                table: "StripInventoryEntries",
                column: "StripCustomerId",
                principalTable: "StripCustomers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StripInventoryEntries_StripCustomers_StripCustomerId",
                table: "StripInventoryEntries");

            migrationBuilder.DropTable(
                name: "StripCustomers");

            migrationBuilder.DropIndex(
                name: "IX_StripInventoryEntries_StripCustomerId",
                table: "StripInventoryEntries");

            migrationBuilder.DropColumn(
                name: "StripCustomerId",
                table: "StripInventoryEntries");
        }
    }
}
