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

    public async Task<IActionResult> Customers(string? searchTerm, int page = 1, int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
        var customersQuery = _context.StripCustomers.AsNoTracking();

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
            .Select(customer => new StripCustomerListItemViewModel
            {
                Id = customer.Id,
                Name = customer.Name,
                Phone = customer.Phone,
                FiveKilogramBalance = customer.Entries
                    .Where(entry => entry.CartonType == StripCartonType.FiveKilogram)
                    .Sum(entry => (int?)(entry.Outgoing - entry.Incoming)) ?? 0,
                NineHundredGramBalance = customer.Entries
                    .Where(entry => entry.CartonType == StripCartonType.NineHundredGram)
                    .Sum(entry => (int?)(entry.Outgoing - entry.Incoming)) ?? 0,
                EntryCount = customer.Entries.Count,
                LastEntryDate = customer.Entries
                    .OrderByDescending(entry => entry.Date)
                    .Select(entry => (DateTime?)entry.Date)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return View(new StripCustomersViewModel
        {
            SearchTerm = searchTerm,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Customers = customers
        });
    }

    public IActionResult CreateCustomer() => View(new StripCustomerInputViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCustomer(StripCustomerInputViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var customer = new StripCustomer
        {
            Name = model.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim()
        };
        _context.StripCustomers.Add(customer);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تمت إضافة عميل الاستربس بنجاح.";

        return RedirectToAction(nameof(CustomerProfile), new { id = customer.Id });
    }

    public async Task<IActionResult> CustomerProfile(int id)
    {
        var customerExists = await _context.StripCustomers.AnyAsync(customer => customer.Id == id);
        if (!customerExists)
            return NotFound();

        return View(await BuildCustomerProfileAsync(id));
    }

    public async Task<IActionResult> EditCustomer(int id)
    {
        var customer = await _context.StripCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);
        if (customer == null)
            return NotFound();

        return View(new StripCustomerInputViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCustomer(StripCustomerInputViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var customer = await _context.StripCustomers.FirstOrDefaultAsync(item => item.Id == model.Id);
        if (customer == null)
            return NotFound();

        customer.Name = model.Name.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تعديل بيانات عميل الاستربس.";

        return RedirectToAction(nameof(Customers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomer(int id, string? searchTerm, int page = 1, int pageSize = 10)
    {
        var customer = await _context.StripCustomers
            .Include(item => item.Entries)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (customer == null)
            return NotFound();

        if (customer.Entries.Count > 0)
        {
            TempData["Error"] = "لا يمكن حذف عميل لديه حركات مسجلة؛ احذف أو صحح حركاته أولًا.";
            return RedirectToAction(nameof(Customers), new { searchTerm, page, pageSize });
        }

        _context.StripCustomers.Remove(customer);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حذف عميل الاستربس.";

        return RedirectToAction(nameof(Customers), new { searchTerm, page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomerEntry(StripCustomerEntryInputViewModel entry)
    {
        var customer = await _context.StripCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == entry.CustomerId);
        if (customer is not { } selectedCustomer)
            return NotFound();

        if (!Enum.IsDefined(entry.CartonType))
            ModelState.AddModelError(nameof(entry.CartonType), "اختر نوع كراتين صحيحًا.");
        if (entry.Incoming == 0 && entry.Outgoing == 0)
            ModelState.AddModelError(string.Empty, "أدخل عدد الكراتين المستلمة أو المرتجعة.");
        if (entry.Incoming > 0 && entry.Outgoing > 0)
            ModelState.AddModelError(string.Empty, "اختر الاستلام أو الإرجاع في الحركة الواحدة.");

        if (Enum.IsDefined(entry.CartonType))
        {
            if (entry.Outgoing > 0)
            {
                var stockBalance = await GetStockBalanceAtDateAsync(entry.CartonType, entry.Date);
                if (entry.Outgoing > stockBalance)
                    ModelState.AddModelError(nameof(entry.Outgoing), $"المتاح في المخزن حتى هذا التاريخ {stockBalance} كرتونة فقط.");
            }

            if (entry.Incoming > 0)
            {
                var customerBalance = await GetCustomerBalanceAtDateAsync(entry.CustomerId, entry.CartonType, entry.Date);
                if (entry.Incoming > customerBalance)
                    ModelState.AddModelError(nameof(entry.Incoming), $"رصيد العميل حتى هذا التاريخ {customerBalance} كرتونة فقط.");
            }
        }

        if (!ModelState.IsValid)
            return View("CustomerProfile", await BuildCustomerProfileAsync(entry.CustomerId, entry));

        _context.StripInventoryEntries.Add(new StripInventoryEntry
        {
            StripCustomerId = selectedCustomer.Id,
            CartonType = entry.CartonType,
            ReceivedBy = selectedCustomer.Name,
            Statement = entry.Statement.Trim(),
            Incoming = entry.Incoming,
            Outgoing = entry.Outgoing,
            Date = entry.Date.Date
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تسجيل حركة العميل وتحديث رصيد المخزن والعميل.";

        return RedirectToAction(nameof(CustomerProfile), new { id = entry.CustomerId });
    }

    public async Task<IActionResult> EditCustomerEntry(int id)
    {
        var entry = await _context.StripInventoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.StripCustomerId != null);
        if (entry == null)
            return NotFound();

        return View(new StripCustomerEntryInputViewModel
        {
            Id = entry.Id,
            CustomerId = entry.StripCustomerId!.Value,
            CartonType = entry.CartonType,
            Statement = entry.Statement,
            Incoming = entry.Incoming,
            Outgoing = entry.Outgoing,
            Date = entry.Date
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCustomerEntry(StripCustomerEntryInputViewModel model)
    {
        var entry = await _context.StripInventoryEntries
            .FirstOrDefaultAsync(item => item.Id == model.Id && item.StripCustomerId == model.CustomerId);
        if (entry == null)
            return NotFound();

        if (!Enum.IsDefined(model.CartonType))
            ModelState.AddModelError(nameof(model.CartonType), "اختر نوع كراتين صحيحًا.");
        if (model.Incoming == 0 && model.Outgoing == 0)
            ModelState.AddModelError(string.Empty, "أدخل عدد الكراتين المستلمة أو المرتجعة.");
        if (model.Incoming > 0 && model.Outgoing > 0)
            ModelState.AddModelError(string.Empty, "اختر الاستلام أو الإرجاع في الحركة الواحدة.");

        if (Enum.IsDefined(model.CartonType))
        {
            if (model.Outgoing > 0)
            {
                var stockBalance = await GetStockBalanceAtDateAsync(model.CartonType, model.Date, entry.Id);
                if (model.Outgoing > stockBalance)
                    ModelState.AddModelError(nameof(model.Outgoing), $"المتاح في المخزن حتى هذا التاريخ {stockBalance} كرتونة فقط.");
            }

            if (model.Incoming > 0)
            {
                var customerBalance = await GetCustomerBalanceAtDateAsync(
                    model.CustomerId, model.CartonType, model.Date, entry.Id);
                if (model.Incoming > customerBalance)
                    ModelState.AddModelError(nameof(model.Incoming), $"رصيد العميل حتى هذا التاريخ {customerBalance} كرتونة فقط.");
            }
        }

        if (!ModelState.IsValid)
            return View(model);

        entry.CartonType = model.CartonType;
        entry.Statement = model.Statement.Trim();
        entry.Incoming = model.Incoming;
        entry.Outgoing = model.Outgoing;
        entry.Date = model.Date.Date;
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تعديل الحركة وتحديث رصيد المخزن والعميل.";

        return RedirectToAction(nameof(CustomerProfile), new { id = model.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomerEntry(int id)
    {
        var entry = await _context.StripInventoryEntries
            .FirstOrDefaultAsync(item => item.Id == id && item.StripCustomerId != null);
        if (entry == null)
            return NotFound();

        var customerId = entry.StripCustomerId!.Value;
        _context.StripInventoryEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حذف الحركة وتحديث رصيد المخزن والعميل.";

        return RedirectToAction(nameof(CustomerProfile), new { id = customerId });
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
            .Include(entry => entry.Customer)
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
                Id = entry.Id,
                CustomerId = entry.StripCustomerId,
                CustomerName = entry.Customer?.Name,
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

    private async Task<StripCustomerProfileViewModel> BuildCustomerProfileAsync(
        int customerId,
        StripCustomerEntryInputViewModel? newEntry = null)
    {
        var customer = await _context.StripCustomers
            .AsNoTracking()
            .FirstAsync(item => item.Id == customerId);
        var entries = await _context.StripInventoryEntries
            .AsNoTracking()
            .Where(entry => entry.StripCustomerId == customerId)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Id)
            .ToListAsync();

        var balances = new Dictionary<StripCartonType, int>
        {
            [StripCartonType.FiveKilogram] = 0,
            [StripCartonType.NineHundredGram] = 0
        };
        var page = new StripCustomerProfileViewModel
        {
            CustomerId = customerId,
            CustomerName = customer.Name,
            Phone = customer.Phone,
            NewEntry = newEntry ?? new StripCustomerEntryInputViewModel
            {
                CustomerId = customerId,
                Date = DateTime.Today
            }
        };

        foreach (var entry in entries)
        {
            balances[entry.CartonType] += entry.Outgoing - entry.Incoming;
            page.Entries.Add(new StripCustomerEntryViewModel
            {
                Id = entry.Id,
                CartonType = entry.CartonType,
                Date = entry.Date,
                Statement = entry.Statement,
                Incoming = entry.Incoming,
                Outgoing = entry.Outgoing,
                Balance = balances[entry.CartonType]
            });
        }

        page.FiveKilogramBalance = balances[StripCartonType.FiveKilogram];
        page.NineHundredGramBalance = balances[StripCartonType.NineHundredGram];
        return page;
    }

    private async Task<int> GetStockBalanceAtDateAsync(
        StripCartonType cartonType,
        DateTime date,
        int? excludedEntryId = null)
    {
        var entries = _context.StripInventoryEntries
            .Where(entry => entry.CartonType == cartonType && entry.Date <= date.Date);
        if (excludedEntryId.HasValue)
            entries = entries.Where(entry => entry.Id != excludedEntryId.Value);

        return await entries.SumAsync(entry => entry.Incoming - entry.Outgoing);
    }

    private async Task<int> GetCustomerBalanceAtDateAsync(
        int customerId,
        StripCartonType cartonType,
        DateTime date,
        int? excludedEntryId = null)
    {
        var entries = _context.StripInventoryEntries
            .Where(entry =>
                entry.StripCustomerId == customerId &&
                entry.CartonType == cartonType &&
                entry.Date <= date.Date);
        if (excludedEntryId.HasValue)
            entries = entries.Where(entry => entry.Id != excludedEntryId.Value);

        return await entries.SumAsync(entry => entry.Outgoing - entry.Incoming);
    }
}
