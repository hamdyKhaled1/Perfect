using HagImamAccounts.Services;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HagImamAccounts.Controllers;

public class CapitalController : Controller
{
    private readonly CapitalService _capitalService;

    public CapitalController(CapitalService capitalService)
    {
        _capitalService = capitalService;
    }

    public async Task<IActionResult> Index()
    {
        var snapshot = await _capitalService.GetSnapshotAsync();

        return View(new CapitalViewModel
        {
            ClientBalances = snapshot.ClientBalances,
            OutsideCustomerBalances = snapshot.OutsideCustomerBalances,
            TreasuryBalance = snapshot.TreasuryBalance,
            NetExpenses = snapshot.NetExpenses,
            NetExpensesWithoutTreasury = snapshot.NetExpensesWithoutTreasury,
            FarmAccount = snapshot.FarmAccount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compare(decimal? manualCapital, string? returnUrl)
    {
        if (!manualCapital.HasValue)
            TempData["CapitalComparisonError"] = "أدخل قيمة رأس المال اليدوي أولًا.";
        else
        {
            var snapshot = await _capitalService.GetSnapshotAsync();
            await _capitalService.SaveManualCapitalAsync(manualCapital.Value, snapshot);
        }

        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }
}
