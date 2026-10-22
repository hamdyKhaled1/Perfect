using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HagImamAccounts.Controllers
{
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExpensesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await BuildLedgerAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEntry([Bind(Prefix = "NewEntry")] ExpenseEntryInputViewModel entry)
        {
            var incoming = entry.Incoming ?? 0;
            var outgoing = entry.Outgoing ?? 0;
            if (incoming == 0 && outgoing == 0)
                ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو الصادر.");
            if (incoming > 0 && outgoing > 0)
                ModelState.AddModelError(string.Empty, "اختر الوارد أو الصادر في الحركة الواحدة.");

            if (!ModelState.IsValid)
            {
                var page = await BuildLedgerAsync();
                page.NewEntry = entry;
                return View("Index", page);
            }

            _context.ExpenseEntries.Add(new ExpenseEntry
            {
                Statement = entry.Statement.Trim(),
                Incoming = incoming,
                Outgoing = outgoing,
                Date = entry.Date.Date
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تسجيل الحركة بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<ExpenseLedgerViewModel> BuildLedgerAsync()
        {
            var entries = await _context.ExpenseEntries
                .AsNoTracking()
                .OrderByDescending(entry => entry.Date)
                .ThenByDescending(entry => entry.Id)
                .ToListAsync();

            return new ExpenseLedgerViewModel
            {
                Entries = entries,
                TotalIncoming = entries.Sum(entry => entry.Incoming),
                TotalOutgoing = entries.Sum(entry => entry.Outgoing)
            };
        }
    }
}
