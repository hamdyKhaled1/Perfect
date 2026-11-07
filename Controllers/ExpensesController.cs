using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace HagImamAccounts.Controllers
{
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExpensesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            return View(await BuildLedgerAsync(page, pageSize));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEntry(
            [Bind(Prefix = "NewEntry")] ExpenseEntryInputViewModel entry,
            int page = 1,
            int pageSize = 10)
        {
            var incoming = entry.Incoming ?? 0;
            var outgoing = entry.Outgoing ?? 0;
            if (incoming == 0 && outgoing == 0)
                ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو الصادر.");
            if (incoming > 0 && outgoing > 0)
                ModelState.AddModelError(string.Empty, "اختر الوارد أو الصادر في الحركة الواحدة.");
            if (outgoing > 0 && !IsValidOutgoingAccount(entry.TreasuryAccount))
                ModelState.AddModelError("NewEntry.TreasuryAccount", "اختر الخزنة التي تم الصرف منها.");

            if (!ModelState.IsValid)
            {
                var ledger = await BuildLedgerAsync(page, pageSize);
                ledger.NewEntry = entry;
                await LoadTreasuryBalancesAsync(entry);
                return View("Index", ledger);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            if (outgoing > 0)
            {
                var account = entry.TreasuryAccount!.Value;
                var availableBalance = await _context.TreasuryEntries
                    .Where(item => item.Account == account)
                    .SumAsync(item => item.Incoming - item.Outgoing);

                if (outgoing > availableBalance)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(
                        string.Empty,
                        $"الرصيد غير كافٍ في {TreasuryEntry.GetAccountName(account)}. الرصيد الحالي: {availableBalance:#,##0.####}.");

                    var ledger = await BuildLedgerAsync(page, pageSize);
                    ledger.NewEntry = entry;
                    await LoadTreasuryBalancesAsync(entry);
                    return View("Index", ledger);
                }
            }

            var expense = new ExpenseEntry
            {
                Statement = entry.Statement.Trim(),
                Incoming = incoming,
                Outgoing = outgoing,
                Date = entry.Date.Date
            };

            if (outgoing > 0)
            {
                expense.TreasuryEntry = new TreasuryEntry
                {
                    Account = entry.TreasuryAccount!.Value,
                    Statement = $"مصروف: {entry.Statement.Trim()}",
                    Outgoing = outgoing,
                    Date = entry.Date.Date
                };
            }

            _context.ExpenseEntries.Add(expense);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = outgoing > 0
                ? "تم تسجيل الحركة وخصمها من الخزنة بنجاح."
                : "تم تسجيل الحركة بنجاح.";

            return RedirectToAction(nameof(Index), new { page = 1, pageSize });
        }

        public async Task<IActionResult> EditEntry(int id, int page = 1, int pageSize = 10)
        {
            var entry = await _context.ExpenseEntries
                .AsNoTracking()
                .Include(item => item.TreasuryEntry)
                .FirstOrDefaultAsync(item => item.Id == id && item.FarmMealBatchId == null && item.PayrollEntryId == null);
            if (entry == null)
                return NotFound();

            var model = new ExpenseEntryInputViewModel
            {
                Id = entry.Id,
                Statement = entry.Statement,
                Incoming = entry.Incoming,
                Outgoing = entry.Outgoing,
                TreasuryAccount = entry.TreasuryEntry?.Account,
                Date = entry.Date,
                Page = Math.Max(page, 1),
                PageSize = NormalizePageSize(pageSize)
            };
            await LoadTreasuryBalancesAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEntry(ExpenseEntryInputViewModel model)
        {
            var incoming = model.Incoming ?? 0;
            var outgoing = model.Outgoing ?? 0;
            if (incoming == 0 && outgoing == 0)
                ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو الصادر.");
            if (incoming > 0 && outgoing > 0)
                ModelState.AddModelError(string.Empty, "اختر الوارد أو الصادر في الحركة الواحدة.");
            if (outgoing > 0 && !IsValidOutgoingAccount(model.TreasuryAccount))
                ModelState.AddModelError(nameof(model.TreasuryAccount), "اختر الخزنة التي تم الصرف منها.");

            var entry = await _context.ExpenseEntries
                .Include(item => item.TreasuryEntry)
                .FirstOrDefaultAsync(item => item.Id == model.Id && item.FarmMealBatchId == null && item.PayrollEntryId == null);
            if (entry == null)
                return NotFound();

            model.Page = Math.Max(model.Page, 1);
            model.PageSize = NormalizePageSize(model.PageSize);
            if (!ModelState.IsValid)
            {
                await LoadTreasuryBalancesAsync(model);
                return View(model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            if (outgoing > 0)
            {
                var account = model.TreasuryAccount!.Value;
                var currentTreasuryEntryId = entry.TreasuryEntryId;
                var availableBalance = await _context.TreasuryEntries
                    .Where(item => item.Account == account &&
                        (currentTreasuryEntryId == null || item.Id != currentTreasuryEntryId))
                    .SumAsync(item => item.Incoming - item.Outgoing);

                if (outgoing > availableBalance)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(
                        string.Empty,
                        $"الرصيد غير كافٍ في {TreasuryEntry.GetAccountName(account)}. الرصيد الحالي: {availableBalance:#,##0.####}.");
                    await LoadTreasuryBalancesAsync(model);
                    return View(model);
                }

                entry.TreasuryEntry ??= new TreasuryEntry();
                entry.TreasuryEntry.Account = account;
                entry.TreasuryEntry.Statement = $"مصروف: {model.Statement.Trim()}";
                entry.TreasuryEntry.Incoming = 0;
                entry.TreasuryEntry.Outgoing = outgoing;
                entry.TreasuryEntry.Date = model.Date.Date;
            }
            else if (entry.TreasuryEntry != null)
            {
                _context.TreasuryEntries.Remove(entry.TreasuryEntry);
                entry.TreasuryEntry = null;
                entry.TreasuryEntryId = null;
            }

            entry.Statement = model.Statement.Trim();
            entry.Incoming = incoming;
            entry.Outgoing = outgoing;
            entry.Date = model.Date.Date;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = "تم تعديل الحركة وتحديث الخزنة بنجاح.";

            return RedirectToAction(nameof(Index), new { page = model.Page, pageSize = model.PageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEntry(int id, int page = 1, int pageSize = 10)
        {
            var entry = await _context.ExpenseEntries
                .Include(item => item.TreasuryEntry)
                .FirstOrDefaultAsync(item => item.Id == id && item.FarmMealBatchId == null && item.PayrollEntryId == null);
            if (entry == null)
                return NotFound();

            if (entry.TreasuryEntry != null)
            {
                _context.TreasuryEntries.Remove(entry.TreasuryEntry);
                entry.TreasuryEntry = null;
                entry.TreasuryEntryId = null;
            }

            _context.ExpenseEntries.Remove(entry);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حذف الحركة وإرجاع المبلغ للخزنة.";

            return RedirectToAction(nameof(Index), new
            {
                page = Math.Max(page, 1),
                pageSize = NormalizePageSize(pageSize)
            });
        }

        private static bool IsValidOutgoingAccount(TreasuryAccount? account) =>
            account.HasValue && ExpenseEntryInputViewModel.OutgoingTreasuryAccounts.Contains(account.Value);

        private async Task<ExpenseLedgerViewModel> BuildLedgerAsync(int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = NormalizePageSize(pageSize);
            var query = _context.ExpenseEntries
                .AsNoTracking()
                .Include(entry => entry.PayrollEntry)
                .Include(entry => entry.TreasuryEntry);
            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);

            var entries = await query
                .OrderByDescending(entry => entry.Date)
                .ThenByDescending(entry => entry.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var remainingAfterEntry = new Dictionary<int, decimal>();
            if (entries.Count > 0)
            {
                var firstEntryOnPage = entries[^1];
                var remainingBeforePage = await query
                    .Where(entry => entry.Date < firstEntryOnPage.Date ||
                        (entry.Date == firstEntryOnPage.Date && entry.Id < firstEntryOnPage.Id))
                    .SumAsync(entry => (decimal?)(entry.Incoming - entry.Outgoing)) ?? 0m;

                foreach (var entry in entries.OrderBy(entry => entry.Date).ThenBy(entry => entry.Id))
                {
                    remainingBeforePage += entry.Incoming - entry.Outgoing;
                    remainingAfterEntry[entry.Id] = remainingBeforePage;
                }
            }

            var ledger = new ExpenseLedgerViewModel
            {
                Entries = entries,
                TotalIncoming = await query.SumAsync(entry => (decimal?)entry.Incoming) ?? 0,
                TotalOutgoing = await query.SumAsync(entry => (decimal?)entry.Outgoing) ?? 0,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                RemainingAfterEntry = remainingAfterEntry
            };
            await LoadTreasuryBalancesAsync(ledger.NewEntry);
            return ledger;
        }

        private async Task LoadTreasuryBalancesAsync(ExpenseEntryInputViewModel model)
        {
            var accounts = ExpenseEntryInputViewModel.OutgoingTreasuryAccounts.ToList();
            var balances = await _context.TreasuryEntries
                .Where(item => accounts.Contains(item.Account))
                .GroupBy(item => item.Account)
                .Select(group => new
                {
                    Account = group.Key,
                    Balance = group.Sum(item => item.Incoming - item.Outgoing)
                })
                .ToListAsync();

            model.TreasuryBalances = balances.ToDictionary(row => row.Account, row => row.Balance);
        }

        private static int NormalizePageSize(int pageSize) =>
            pageSize is 10 or 25 or 50 ? pageSize : 10;
    }
}
