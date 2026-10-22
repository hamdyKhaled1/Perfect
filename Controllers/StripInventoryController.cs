using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HagImamAccounts.Controllers;

public class StripInventoryController : Controller
{
    private readonly ApplicationDbContext _context;

    public StripInventoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var balances = await _context.StripInventoryEntries
            .AsNoTracking()
            .GroupBy(entry => entry.CartonType)
            .Select(group => new
            {
                CartonType = group.Key,
                Balance = group.Sum(entry => entry.Incoming - entry.Outgoing)
            })
            .ToDictionaryAsync(item => item.CartonType, item => item.Balance);

        balances.TryGetValue(StripCartonType.FiveKilogram, out var fiveKilogramBalance);
        balances.TryGetValue(StripCartonType.NineHundredGram, out var nineHundredGramBalance);

        return View(new StripInventoryOverviewViewModel
        {
            FiveKilogramBalance = fiveKilogramBalance,
            NineHundredGramBalance = nineHundredGramBalance
        });
    }

    public async Task<IActionResult> Details(StripCartonType cartonType)
    {
        if (!Enum.IsDefined(cartonType))
            return NotFound();

        return View(await BuildPageAsync(cartonType));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEntry(
        [Bind(Prefix = "NewEntry")] StripInventoryEntryInputViewModel entry)
    {
        if (!Enum.IsDefined(entry.CartonType))
            return BadRequest();

        if (entry.Incoming == 0 && entry.Outgoing == 0)
            ModelState.AddModelError(string.Empty, "أدخل عدد الكراتين الواردة أو المسحوبة.");
        if (entry.Incoming > 0 && entry.Outgoing > 0)
            ModelState.AddModelError(string.Empty, "اختر الوارد أو المسحوب في الحركة الواحدة.");

        var availableBalance = await _context.StripInventoryEntries
            .Where(item => item.CartonType == entry.CartonType && item.Date <= entry.Date.Date)
            .SumAsync(item => item.Incoming - item.Outgoing);

        if (entry.Outgoing > availableBalance)
            ModelState.AddModelError(
                "NewEntry.Outgoing",
                $"الرصيد المتاح حتى التاريخ المحدد {availableBalance} كرتونة فقط.");

        if (!ModelState.IsValid)
        {
            var page = await BuildPageAsync(entry.CartonType);
            page.NewEntry = entry;
            return View("Index", page);
        }

        _context.StripInventoryEntries.Add(new StripInventoryEntry
        {
            CartonType = entry.CartonType,
            ReceivedBy = entry.ReceivedBy.Trim(),
            Statement = entry.Statement.Trim(),
            Incoming = entry.Incoming,
            Outgoing = entry.Outgoing,
            Date = entry.Date.Date
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تسجيل حركة الاستربس وتحديث الرصيد.";

        return RedirectToAction(nameof(Details), new { cartonType = entry.CartonType });
    }

    private async Task<StripInventoryPageViewModel> BuildPageAsync(
        StripCartonType cartonType,
        StripInventoryEntryInputViewModel? newEntry = null)
    {
        var allEntries = await _context.StripInventoryEntries
            .AsNoTracking()
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Id)
            .ToListAsync();

        var page = new StripInventoryPageViewModel
        {
            SelectedCartonType = cartonType,
            FiveKilogramBalance = allEntries
                .Where(entry => entry.CartonType == StripCartonType.FiveKilogram)
                .Sum(entry => entry.Incoming - entry.Outgoing),
            NineHundredGramBalance = allEntries
                .Where(entry => entry.CartonType == StripCartonType.NineHundredGram)
                .Sum(entry => entry.Incoming - entry.Outgoing),
            NewEntry = newEntry ?? new StripInventoryEntryInputViewModel
            {
                CartonType = cartonType,
                Date = DateTime.Today
            }
        };

        var balance = 0;
        foreach (var entry in allEntries.Where(entry => entry.CartonType == cartonType))
        {
            balance += entry.Incoming - entry.Outgoing;
            page.Entries.Add(new StripInventoryEntryViewModel
            {
                Date = entry.Date,
                ReceivedBy = entry.ReceivedBy,
                Statement = entry.Statement,
                Incoming = entry.Incoming,
                Outgoing = entry.Outgoing,
                Balance = balance
            });
        }

        return page;
    }
}
