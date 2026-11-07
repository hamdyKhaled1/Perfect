using HagImamAccounts.Data;
using HagImamAccounts.Models;
using Microsoft.EntityFrameworkCore;

namespace HagImamAccounts.Services;

public sealed record CapitalSnapshot(
    decimal ClientBalances,
    decimal OutsideCustomerBalances,
    decimal TreasuryBalance,
    decimal NetExpenses,
    decimal NetExpensesWithoutTreasury,
    decimal FarmAccount)
{
    /// <summary>رأس المال المحسوب من أرصدة الحسابات دون تسويات يدوية.</summary>
    public decimal ComputedCapital =>
        ClientBalances + OutsideCustomerBalances + TreasuryBalance + NetExpensesWithoutTreasury + FarmAccount;

    public decimal TotalCapital => ComputedCapital;
}

/// <summary>
/// يحسب مكونات رأس مال الشركة (نفس معادلة صفحة رأس المال) لاستخدامها في الشريط العلوي لكل الصفحات.
/// </summary>
public class CapitalService
{
    private readonly ApplicationDbContext _context;

    public CapitalService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CapitalSnapshot> GetSnapshotAsync()
    {
        // نفس منطق "إجمالي المستحقات" في الشاشة الرئيسية (صفحة العملاء):
        // العملاء النشطون فقط، ولكل عميل الرصيد القديم من أول حركة فقط
        // ثم مجموع حركاته، والتلاجة تُحسب من دفتر التلاجة.
        // (لا يجوز جمع OldBalance من كل الحركات لأنه مُرحّل في كل حركة.)
        var clientBalancesList = await _context.Clients
            .AsNoTracking()
            .Where(client => client.IsActive)
            .Select(client => client.IsFridge
                ? (client.FridgeEntries
                    .Where(entry => entry.Source != FridgeEntrySource.StripSale)
                    .Sum(entry => (decimal?)(entry.Outgoing - entry.Incoming)) ?? 0m)
                : (client.Transactions
                        .Where(transaction => !transaction.IsDeleted)
                        .OrderBy(transaction => transaction.Id)
                        .Select(transaction => (decimal?)transaction.OldBalance)
                        .FirstOrDefault() ?? 0m)
                    + (client.Transactions
                        .Where(transaction => !transaction.IsDeleted)
                        .Sum(transaction => (decimal?)(
                            (transaction.GrossWeight - transaction.EmptyWeight) * transaction.PricePerKilo
                            + transaction.AdditionAmount
                            - transaction.CashCollection
                            - transaction.ElectronicCollection
                            - transaction.DiscountAmount)) ?? 0m))
            .ToListAsync();

        var clientBalances = clientBalancesList.Sum();

        var outsideCustomerBalances = await _context.OutsideEntries
            .SumAsync(entry => entry.Taken - entry.Incoming);

        var treasuryBalance = await _context.TreasuryEntries
            .SumAsync(entry => entry.Incoming - entry.Outgoing);

        var netExpenses = await _context.ExpenseEntries
            .SumAsync(entry => entry.Outgoing - entry.Incoming);

        // المصروفات المسددة من خزنة أو محفظة تم خصمها بالفعل من رصيد الخزن،
        // لذلك لا تُخصم مرة أخرى عند حساب رأس المال.
        var netExpensesWithoutTreasury = await _context.ExpenseEntries
            .Where(entry => entry.TreasuryEntryId == null)
            .SumAsync(entry => entry.Outgoing - entry.Incoming);

        var farmAmountDue = await _context.FarmMealBatches
            .SumAsync(batch => batch.FarmPrice * batch.FarmWeight
                + batch.FarmAdditionAmount
                - batch.FarmDiscountAmount
                - batch.FarmPaidAmount);
        farmAmountDue -= await _context.FarmPayments
            .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
        var farmAccount = -farmAmountDue;

        return new CapitalSnapshot(
            clientBalances,
            outsideCustomerBalances,
            treasuryBalance,
            netExpenses,
            netExpensesWithoutTreasury,
            farmAccount);
    }

    public Task<CapitalSetting?> GetManualCapitalAsync() =>
        _context.CapitalSettings.AsNoTracking().SingleOrDefaultAsync(setting => setting.Id == 1);

    public async Task SaveManualCapitalAsync(decimal manualCapital, CapitalSnapshot snapshot)
    {
        var setting = await _context.CapitalSettings.FindAsync(1);
        if (setting == null)
        {
            setting = new CapitalSetting { Id = 1 };
            _context.CapitalSettings.Add(setting);
        }

        setting.ManualCapital = manualCapital;
        setting.ClientBalances = snapshot.ClientBalances;
        setting.OutsideCustomerBalances = snapshot.OutsideCustomerBalances;
        setting.TreasuryBalance = snapshot.TreasuryBalance;
        setting.NetExpensesWithoutTreasury = snapshot.NetExpensesWithoutTreasury;
        setting.FarmAccount = snapshot.FarmAccount;

        await _context.SaveChangesAsync();
    }
}
