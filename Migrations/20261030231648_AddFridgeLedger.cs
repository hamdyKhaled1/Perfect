using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HagImamAccounts.Migrations
{
    /// <inheritdoc />
    public partial class AddFridgeLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFridge",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FridgeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Incoming = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Outgoing = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    StripInventoryEntryId = table.Column<int>(type: "int", nullable: true),
                    StripCustomerId = table.Column<int>(type: "int", nullable: true),
                    TreasuryEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FridgeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FridgeEntries_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FridgeEntries_StripCustomers_StripCustomerId",
                        column: x => x.StripCustomerId,
                        principalTable: "StripCustomers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FridgeEntries_StripInventoryEntries_StripInventoryEntryId",
                        column: x => x.StripInventoryEntryId,
                        principalTable: "StripInventoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FridgeEntries_TreasuryEntries_TreasuryEntryId",
                        column: x => x.TreasuryEntryId,
                        principalTable: "TreasuryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsFridge",
                value: false);

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsFridge",
                value: false);

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsFridge",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_FridgeEntries_ClientId_Date_Id",
                table: "FridgeEntries",
                columns: new[] { "ClientId", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FridgeEntries_StripCustomerId",
                table: "FridgeEntries",
                column: "StripCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_FridgeEntries_StripInventoryEntryId",
                table: "FridgeEntries",
                column: "StripInventoryEntryId",
                unique: true,
                filter: "[StripInventoryEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FridgeEntries_TreasuryEntryId",
                table: "FridgeEntries",
                column: "TreasuryEntryId");

            // إنشاء عميل "التلاجة" مرة واحدة فقط (أو تحويل عميل بنفس الاسم إلى التلاجة).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Clients] WHERE [IsFridge] = 1)
BEGIN
    DECLARE @existingFridgeClientId int = (
        SELECT TOP(1) [Id] FROM [Clients] WHERE [Name] = N'التلاجة' ORDER BY [Id]);

    IF @existingFridgeClientId IS NOT NULL
    BEGIN
        UPDATE [Clients]
        SET [IsFridge] = 1, [IsActive] = 1
        WHERE [Id] = @existingFridgeClientId;
    END
    ELSE
    BEGIN
        INSERT INTO [Clients]
            ([Name], [Phone], [Notes], [CreatedAt], [IsActive], [IsFridge], [DefaultPriceIncrement], [DefaultPanehPriceIncrement])
        VALUES
            (N'التلاجة', NULL, N'حساب كل عملاء الاستربس', SYSDATETIME(), 1, 1, 0, 0);
    END
END;
");

            // ترحيل حركات عملاء الاستربس الحالية إلى دفتر التلاجة (بدون إضافة تحصيلات قديمة للخزنة لتجنب الازدواج).
            migrationBuilder.Sql(@"
DECLARE @fridgeClientId int = (SELECT TOP(1) [Id] FROM [Clients] WHERE [IsFridge] = 1);

IF @fridgeClientId IS NOT NULL
BEGIN
    INSERT INTO [FridgeEntries]
        ([ClientId], [Date], [Statement], [Incoming], [Outgoing], [Source], [StripInventoryEntryId], [StripCustomerId], [TreasuryEntryId])
    SELECT
        @fridgeClientId,
        entry.[Date],
        LEFT(CASE
                WHEN entry.[EntryType] = 1
                    THEN N'تحصيل من عميل الاستربس ' + COALESCE(customer.[Name], entry.[ReceivedBy]) + N': ' + entry.[Statement]
                ELSE N'بيع لعميل الاستربس ' + COALESCE(customer.[Name], entry.[ReceivedBy]) + N': ' + entry.[Statement]
             END, 500),
        CASE WHEN entry.[EntryType] = 1 THEN entry.[PaidAmount] ELSE 0 END,
        CASE WHEN entry.[EntryType] = 0
             THEN (CASE WHEN entry.[Amount] > 0 THEN entry.[Amount] ELSE entry.[Cartons] * entry.[PricePerCarton] END)
             ELSE 0 END,
        CASE WHEN entry.[EntryType] = 1 THEN 2 ELSE 1 END,
        entry.[Id],
        entry.[StripCustomerId],
        NULL
    FROM [StripInventoryEntries] AS entry
    LEFT JOIN [StripCustomers] AS customer ON customer.[Id] = entry.[StripCustomerId]
    WHERE entry.[StripCustomerId] IS NOT NULL
      AND entry.[EntryType] IN (0, 1)
      AND NOT EXISTS (
          SELECT 1 FROM [FridgeEntries] AS fridge
          WHERE fridge.[StripInventoryEntryId] = entry.[Id]);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FridgeEntries");

            migrationBuilder.DropColumn(
                name: "IsFridge",
                table: "Clients");
        }
    }
}
