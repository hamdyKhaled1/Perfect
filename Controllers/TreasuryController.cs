using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace HagImamAccounts.Controllers
{
    public class TreasuryController : Controller
    {
        private static readonly TreasuryAccount[] Accounts =
        {
            TreasuryAccount.AbuMazenCash,
            TreasuryAccount.HajjImamCash,
            TreasuryAccount.AbuMazenVodafoneCash,
            TreasuryAccount.AbuMazenInstaPay,
            TreasuryAccount.HajjImamVodafoneCash,
            TreasuryAccount.HajjImamInstaPay,
            TreasuryAccount.General
        };

        private readonly ApplicationDbContext _context;

        public TreasuryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var balances = await _context.TreasuryEntries
                .AsNoTracking()
                .GroupBy(entry => entry.Account)
                .Select(group => new
                {
                    Account = group.Key,
                    Balance = group.Sum(entry => entry.Incoming - entry.Outgoing),
                    EntryCount = group.Count()
                })
                .ToDictionaryAsync(item => item.Account);

            var accounts = Accounts.Select(account =>
            {
                balances.TryGetValue(account, out var summary);
                return new TreasuryAccountSummaryViewModel
                {
                    Account = account,
                    AccountName = AccountName(account),
                    Balance = summary?.Balance ?? 0,
                    EntryCount = summary?.EntryCount ?? 0
                };
            }).ToList();

            return View(accounts);
        }

        public async Task<IActionResult> Account(TreasuryAccount account)
        {
            if (!IsValidAccount(account))
                return NotFound();

            return View(await BuildAccountPageAsync(account));
        }

        public async Task<IActionResult> EditEntry(TreasuryAccount account, int id)
        {
            if (!IsValidAccount(account))
                return NotFound();

            var entry = await _context.TreasuryEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id && item.Account == account);
            if (entry == null)
                return NotFound();

            return View(new TreasuryMovementViewModel
            {
                Account = account,
                AccountName = AccountName(account),
                NewEntry = new TreasuryEntryInputViewModel
                {
                    Id = entry.Id,
                    Statement = GetEntryStatement(entry),
                    Incoming = entry.TransferId.HasValue ? null : entry.Incoming,
                    Outgoing = entry.TransferId.HasValue ? null : entry.Outgoing,
                    Date = entry.Date
                },
                NewTransfer = new TreasuryTransferInputViewModel
                {
                    Amount = entry.TransferId.HasValue
                        ? entry.Incoming > 0 ? entry.Incoming : entry.Outgoing
                        : 0,
                    Statement = entry.TransferId.HasValue ? GetTransferStatement(entry) : string.Empty,
                    Date = entry.Date
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEntry(
            TreasuryAccount account,
            [Bind(Prefix = "NewEntry")] TreasuryEntryInputViewModel entry)
        {
            if (!IsValidAccount(account))
                return NotFound();

            var incoming = entry.Incoming ?? 0;
            var outgoing = entry.Outgoing ?? 0;
            if (incoming == 0 && outgoing == 0)
                ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو الصادر.");
            if (incoming > 0 && outgoing > 0)
                ModelState.AddModelError(string.Empty, "اختر الوارد أو الصادر في الحركة الواحدة.");
            if (account == TreasuryAccount.General && incoming > 0)
                ModelState.AddModelError(nameof(entry.Incoming), "الوارد للخزنة العمومية يتم من خلال تحويل من خزنة أو محفظة أخرى.");

            if (!ModelState.IsValid)
            {
                var page = await BuildAccountPageAsync(account);
                page.NewEntry = entry;
                return View("Account", page);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (outgoing > 0)
            {
                var availableBalance = await _context.TreasuryEntries
                    .Where(item => item.Account == account)
                    .SumAsync(item => item.Incoming - item.Outgoing);

                if (outgoing > availableBalance)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, $"الرصيد غير كافٍ. الرصيد الحالي: {availableBalance:#,##0.####}.");

                    var page = await BuildAccountPageAsync(account);
                    page.NewEntry = entry;
                    return View("Account", page);
                }
            }

            _context.TreasuryEntries.Add(new TreasuryEntry
            {
                Account = account,
                Statement = entry.Statement.Trim(),
                Incoming = incoming,
                Outgoing = outgoing,
                Date = entry.Date.Date
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = "تم تسجيل الحركة في الخزنة بنجاح.";

            return RedirectToAction(nameof(Account), new { account });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferToGeneral(
            TreasuryAccount account,
            [Bind(Prefix = "NewTransfer")] TreasuryTransferInputViewModel transfer)
        {
            if (!IsValidAccount(account) || account == TreasuryAccount.General)
                return NotFound();

            if (!ModelState.IsValid)
            {
                var page = await BuildAccountPageAsync(account);
                page.NewTransfer = transfer;
                return View("Account", page);
            }

            var transferId = Guid.NewGuid();
            var statement = transfer.Statement.Trim();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var availableBalance = await _context.TreasuryEntries
                .Where(entry => entry.Account == account)
                .SumAsync(entry => entry.Incoming - entry.Outgoing);

            if (availableBalance <= 0 || transfer.Amount > availableBalance)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(
                    string.Empty,
                    availableBalance <= 0
                        ? $"لا يمكن التحويل؛ رصيد الخزنة غير كافٍ (الرصيد الحالي: {availableBalance:#,##0.####})."
                        : $"مبلغ التحويل أكبر من رصيد الخزنة. الرصيد المتاح: {availableBalance:#,##0.####}.");

                var page = await BuildAccountPageAsync(account);
                page.NewTransfer = transfer;
                return View("Account", page);
            }

            _context.TreasuryEntries.AddRange(
                new TreasuryEntry
                {
                    Account = account,
                    Statement = $"تحويل إلى الخزنة العمومية: {statement}",
                    Outgoing = transfer.Amount,
                    Date = transfer.Date.Date,
                    TransferId = transferId
                },
                new TreasuryEntry
                {
                    Account = TreasuryAccount.General,
                    Statement = $"تحويل من {AccountName(account)}: {statement}",
                    Incoming = transfer.Amount,
                    Date = transfer.Date.Date,
                    TransferId = transferId
                });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم التحويل إلى الخزنة العمومية وتسجيل طرفيه.";
            return RedirectToAction(nameof(Account), new { account });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEntry(
            TreasuryAccount account,
            [Bind(Prefix = "NewEntry")] TreasuryEntryInputViewModel entry,
            [Bind(Prefix = "NewTransfer")] TreasuryTransferInputViewModel transfer)
        {
            if (!IsValidAccount(account))
                return NotFound();

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var existingEntry = await _context.TreasuryEntries
                .FirstOrDefaultAsync(item => item.Id == entry.Id && item.Account == account);
            if (existingEntry == null)
                return NotFound();

            var isTransfer = existingEntry.TransferId.HasValue;
            if (isTransfer)
            {
                ModelState.Remove("NewEntry.Statement");
                ModelState.Remove("NewEntry.Incoming");
                ModelState.Remove("NewEntry.Outgoing");
                if (!ModelState.IsValid)
                    return View(BuildEditViewModel(account, entry, transfer));
            }
            else
            {
                ModelState.Remove("NewTransfer.Amount");
                ModelState.Remove("NewTransfer.Statement");
                var incoming = entry.Incoming ?? 0;
                var outgoing = entry.Outgoing ?? 0;
                if (incoming == 0 && outgoing == 0)
                    ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو الصادر.");
                if (incoming > 0 && outgoing > 0)
                    ModelState.AddModelError(string.Empty, "اختر الوارد أو الصادر في الحركة الواحدة.");
                if (account == TreasuryAccount.General && incoming > 0)
                    ModelState.AddModelError(nameof(entry.Incoming), "الوارد للخزنة العمومية يتم من خلال تحويل من خزنة أو محفظة أخرى.");

                if (!ModelState.IsValid)
                    return View(BuildEditViewModel(account, entry, transfer));
            }

            var statement = entry.Statement.Trim();
            if (isTransfer)
            {
                var pairedEntry = await _context.TreasuryEntries
                    .FirstOrDefaultAsync(item =>
                        item.TransferId == existingEntry.TransferId &&
                        item.Id != existingEntry.Id);
                if (pairedEntry == null)
                {
                    ModelState.AddModelError(string.Empty, "لم يتم العثور على الطرف المقابل للتحويل.");
                    return View(BuildEditViewModel(account, entry, transfer));
                }

                var isSourceEntry = existingEntry.Outgoing > 0;
                var sourceAccount = isSourceEntry ? account : pairedEntry.Account;
                var sourceEntryId = isSourceEntry ? existingEntry.Id : pairedEntry.Id;
                var availableBalance = await _context.TreasuryEntries
                    .Where(item => item.Account == sourceAccount && item.Id != sourceEntryId)
                    .SumAsync(item => item.Incoming - item.Outgoing);
                if (transfer.Amount > availableBalance)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, $"الرصيد غير كافٍ للتحويل. الرصيد المتاح: {availableBalance:#,##0.####}.");
                    return View(BuildEditViewModel(account, entry, transfer));
                }

                var transferStatement = transfer.Statement.Trim();
                existingEntry.Statement = isSourceEntry
                    ? $"تحويل إلى الخزنة العمومية: {transferStatement}"
                    : $"تحويل من {AccountName(pairedEntry.Account)}: {transferStatement}";
                existingEntry.Date = transfer.Date.Date;
                pairedEntry.Statement = isSourceEntry
                    ? $"تحويل من {AccountName(account)}: {transferStatement}"
                    : $"تحويل إلى الخزنة العمومية: {transferStatement}";
                pairedEntry.Date = transfer.Date.Date;
                if (isSourceEntry)
                {
                    existingEntry.Outgoing = transfer.Amount;
                    pairedEntry.Incoming = transfer.Amount;
                }
                else
                {
                    existingEntry.Incoming = transfer.Amount;
                    pairedEntry.Outgoing = transfer.Amount;
                }
            }
            else
            {
                var outgoing = entry.Outgoing ?? 0;
                var availableBalance = await _context.TreasuryEntries
                    .Where(item => item.Account == account && item.Id != existingEntry.Id)
                    .SumAsync(item => item.Incoming - item.Outgoing);
                if (outgoing > availableBalance)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, $"الرصيد غير كافٍ. الرصيد الحالي: {availableBalance:#,##0.####}.");
                    return View(BuildEditViewModel(account, entry, transfer));
                }

                existingEntry.Statement = statement;
                existingEntry.Incoming = entry.Incoming ?? 0;
                existingEntry.Outgoing = outgoing;
                existingEntry.Date = entry.Date.Date;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = isTransfer
                ? "تم تعديل التحويل وتحديث طرفيه."
                : "تم تعديل حركة الخزنة.";
            return RedirectToAction(nameof(Account), new { account });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEntry(TreasuryAccount account, int id)
        {
            if (!IsValidAccount(account))
                return NotFound();

            var entry = await _context.TreasuryEntries
                .FirstOrDefaultAsync(item => item.Id == id && item.Account == account);
            if (entry == null)
                return NotFound();

            if (entry.TransferId.HasValue)
            {
                var transferEntries = await _context.TreasuryEntries
                    .Where(item => item.TransferId == entry.TransferId)
                    .ToListAsync();
                _context.TreasuryEntries.RemoveRange(transferEntries);
            }
            else
            {
                _context.TreasuryEntries.Remove(entry);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = entry.TransferId.HasValue
                ? "تم حذف التحويل من طرفيه."
                : "تم حذف حركة الخزنة.";
            return RedirectToAction(nameof(Account), new { account });
        }

        private async Task<TreasuryMovementViewModel> BuildAccountPageAsync(TreasuryAccount account)
        {
            var entries = await _context.TreasuryEntries
                .AsNoTracking()
                .Where(entry => entry.Account == account)
                .OrderBy(entry => entry.Date)
                .ThenBy(entry => entry.Id)
                .ToListAsync();

            decimal balance = 0;
            var rows = new List<TreasuryMovementRowViewModel>(entries.Count);
            foreach (var entry in entries)
            {
                balance += entry.Incoming - entry.Outgoing;
                rows.Add(new TreasuryMovementRowViewModel
                {
                    Id = entry.Id,
                    Account = entry.Account,
                    Date = entry.Date,
                    Statement = entry.Statement,
                    Incoming = entry.Incoming,
                    Outgoing = entry.Outgoing,
                    Balance = balance,
                    IsTransfer = entry.TransferId.HasValue
                });
            }
            rows.Reverse();

            return new TreasuryMovementViewModel
            {
                Account = account,
                AccountName = AccountName(account),
                Balance = balance,
                Entries = rows
            };
        }

        private TreasuryMovementViewModel BuildEditViewModel(
            TreasuryAccount account,
            TreasuryEntryInputViewModel entry,
            TreasuryTransferInputViewModel transfer) =>
            new()
            {
                Account = account,
                AccountName = AccountName(account),
                NewEntry = entry,
                NewTransfer = transfer
            };

        private static string GetEntryStatement(TreasuryEntry entry)
        {
            if (!entry.TransferId.HasValue)
                return entry.Statement;

            const string sourcePrefix = "تحويل إلى الخزنة العمومية: ";
            const string generalPrefix = "تحويل من ";
            if (entry.Statement.StartsWith(sourcePrefix, StringComparison.Ordinal))
                return entry.Statement[sourcePrefix.Length..];
            var separatorIndex = entry.Statement.IndexOf(": ", StringComparison.Ordinal);
            return entry.Statement.StartsWith(generalPrefix, StringComparison.Ordinal) && separatorIndex >= 0
                ? entry.Statement[(separatorIndex + 2)..]
                : entry.Statement;
        }

        private static string GetTransferStatement(TreasuryEntry entry) => GetEntryStatement(entry);

        private static bool IsValidAccount(TreasuryAccount account) =>
            Enum.IsDefined(account) && Accounts.Contains(account);

        private static string AccountName(TreasuryAccount account) => account switch
        {
            TreasuryAccount.AbuMazenCash => "خزنة أبو مازن - نقدي",
            TreasuryAccount.HajjImamCash => "خزنة الحاج إمام - نقدي",
            TreasuryAccount.AbuMazenVodafoneCash => "فودافون كاش - أبو مازن",
            TreasuryAccount.AbuMazenInstaPay => "إنستا باي - أبو مازن",
            TreasuryAccount.HajjImamVodafoneCash => "فودافون كاش - الحاج إمام",
            TreasuryAccount.HajjImamInstaPay => "إنستا باي - الحاج إمام",
            TreasuryAccount.General => "الخزنة العمومية",
            _ => throw new ArgumentOutOfRangeException(nameof(account))
        };
    }
}
