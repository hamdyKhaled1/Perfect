using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HagImamAccounts.Controllers
{
    public class OutsideController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OutsideController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
            var customersQuery = _context.OutsideCustomers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                customersQuery = customersQuery.Where(customer =>
                    customer.Name.Contains(searchTerm) ||
                    (customer.Phone != null && customer.Phone.Contains(searchTerm)));
            }

            var totalCount = await customersQuery.CountAsync();
            page = totalCount == 0 ? 1 : Math.Min(page, (int)Math.Ceiling(totalCount / (double)pageSize));
            var customers = await customersQuery
                .OrderBy(customer => customer.Name)
                .ThenBy(customer => customer.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(customer => new OutsideCustomerListItemViewModel
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    Phone = customer.Phone,
                    EntryCount = customer.Entries.Count,
                    LastEntryDate = customer.Entries
                        .OrderByDescending(entry => entry.Date)
                        .Select(entry => (DateTime?)entry.Date)
                        .FirstOrDefault(),
                    Balance = customer.Entries.Sum(entry => (decimal?)(entry.Taken - entry.Incoming)) ?? 0
                })
                .ToListAsync();

            return View(new OutsideCustomersViewModel
            {
                SearchTerm = searchTerm,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Customers = customers
            });
        }

        public IActionResult Create()
        {
            return View(new OutsideCustomerInputViewModel());
        }

        public async Task<IActionResult> Profile(int id)
        {
            var customerExists = await _context.OutsideCustomers
                .AnyAsync(customer => customer.Id == id);
            if (!customerExists)
                return NotFound();

            return View(await BuildPageAsync(id));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _context.OutsideCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
            if (customer == null)
                return NotFound();

            return View(new OutsideCustomerInputViewModel
            {
                Id = customer.Id,
                Name = customer.Name,
                Phone = customer.Phone
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OutsideCustomerInputViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var customer = await _context.OutsideCustomers
                .FirstOrDefaultAsync(item => item.Id == model.Id);
            if (customer == null)
                return NotFound();

            customer.Name = model.Name.Trim();
            customer.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تعديل بيانات عميل الخارج.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? searchTerm, int page = 1, int pageSize = 10)
        {
            var customer = await _context.OutsideCustomers
                .FirstOrDefaultAsync(item => item.Id == id);
            if (customer == null)
                return NotFound();

            _context.OutsideCustomers.Remove(customer);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حذف عميل الخارج وحركاته.";

            return RedirectToAction(nameof(Index), new { searchTerm, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCustomer(OutsideCustomerInputViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Create", model);

            var customer = new OutsideCustomer
            {
                Name = model.Name.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone)
                    ? null
                    : model.Phone.Trim()
            };

            _context.OutsideCustomers.Add(customer);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تمت إضافة عميل الخارج بنجاح.";

            return RedirectToAction(nameof(Profile), new { id = customer.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEntry(OutsideEntryInputViewModel entry)
        {
            var customerExists = await _context.OutsideCustomers
                .AnyAsync(customer => customer.Id == entry.CustomerId);

            if (!customerExists)
                ModelState.AddModelError(string.Empty, "اختر عميلًا صحيحًا من قائمة عملاء الخارج.");

            if (entry.Incoming == 0 && entry.Taken == 0)
                ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو في خانة هو أخد.");

            if (!ModelState.IsValid)
            {
                var page = await BuildPageAsync(entry.CustomerId, newEntry: entry);
                return View("Profile", page);
            }

            _context.OutsideEntries.Add(new OutsideEntry
            {
                OutsideCustomerId = entry.CustomerId,
                Statement = entry.Statement.Trim(),
                Incoming = entry.Incoming,
                Taken = entry.Taken,
                Date = entry.Date.Date
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تسجيل الحركة بنجاح.";

            return RedirectToAction(nameof(Profile), new { id = entry.CustomerId });
        }

        private async Task<OutsidePageViewModel> BuildPageAsync(
            int? customerId = null,
            OutsideCustomerInputViewModel? newCustomer = null,
            OutsideEntryInputViewModel? newEntry = null)
        {
            var customers = await _context.OutsideCustomers
                .AsNoTracking()
                .OrderBy(customer => customer.Name)
                .ToListAsync();

            var selectedCustomer = customerId.HasValue
                ? customers.FirstOrDefault(customer => customer.Id == customerId.Value)
                : null;

            var page = new OutsidePageViewModel
            {
                Customers = customers,
                SelectedCustomerId = selectedCustomer?.Id,
                SelectedCustomerName = selectedCustomer?.Name,
                NewCustomer = newCustomer ?? new OutsideCustomerInputViewModel(),
                NewEntry = newEntry ?? new OutsideEntryInputViewModel
                {
                    CustomerId = selectedCustomer?.Id ?? 0,
                    Date = DateTime.Today
                }
            };

            if (selectedCustomer == null)
                return page;

            var entries = await _context.OutsideEntries
                .AsNoTracking()
                .Where(entry => entry.OutsideCustomerId == selectedCustomer.Id)
                .OrderBy(entry => entry.Date)
                .ThenBy(entry => entry.Id)
                .ToListAsync();

            decimal balance = 0;
            foreach (var entry in entries)
            {
                balance += entry.Taken - entry.Incoming;
                page.Entries.Add(new OutsideEntryViewModel
                {
                    Date = entry.Date,
                    Statement = entry.Statement,
                    Incoming = entry.Incoming,
                    Taken = entry.Taken,
                    Balance = balance
                });
            }

            page.RemainingBalance = balance;
            return page;
        }
    }
}
