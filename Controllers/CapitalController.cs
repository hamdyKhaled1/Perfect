using HagImamAccounts.Data;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HagImamAccounts.Controllers;

public class CapitalController : Controller
{
    private readonly ApplicationDbContext _context;

    public CapitalController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var clientBalances = await _context.Transactions
            .Where(transaction => !transaction.IsDeleted)
            .SumAsync(transaction =>
                (transaction.GrossWeight - transaction.EmptyWeight) * transaction.PricePerKilo
                + transaction.OldBalance
                - transaction.CashCollection
                - transaction.ElectronicCollection
                + transaction.AdditionAmount
                - transaction.DiscountAmount);

        var outsideCustomerBalances = await _context.OutsideEntries
            .SumAsync(entry => entry.Taken - entry.Incoming);

        var treasuryBalance = await _context.TreasuryEntries
            .SumAsync(entry => entry.Incoming - entry.Outgoing);

        var netExpenses = await _context.ExpenseEntries
            .SumAsync(entry => entry.Outgoing - entry.Incoming);

        var farmAccount = await _context.FarmMealBatches
            .SumAsync(batch => batch.FarmPrice * batch.FarmWeight - batch.FarmPaidAmount);

        return View(new CapitalViewModel
        {
            ClientBalances = clientBalances,
            OutsideCustomerBalances = outsideCustomerBalances,
            TreasuryBalance = treasuryBalance,
            NetExpenses = netExpenses,
            FarmAccount = farmAccount
        });
    }
}
