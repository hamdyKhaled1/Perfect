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

    public async Task<IActionResult> Sales(DateTime? fromDate = null, DateTime? toDate = null, int? customerId = null)
    {
        var query = _context.StripInventoryEntries
            .AsNoTracking()
            .Include(entry => entry.Customer)
            .Where(entry => entry.StripCustomerId != null && entry.EntryType == StripEntryType.Sale);

        if (fromDate.HasValue)
            query = query.Where(entry => entry.Date >= fromDate.Value.Date);
        if (toDate.HasValue)
            query = query.Where(entry => entry.Date <= toDate.Value.Date);
        if (customerId.HasValue)
            query = query.Where(entry => entry.StripCustomerId == customerId.Value);

        var rows = await query
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new StripSalesRowViewModel
            {
                Id = entry.Id,
                Date = entry.Date,
                CustomerId = entry.StripCustomerId!.Value,
                CustomerName = entry.Customer != null ? entry.Customer.Name : entry.ReceivedBy,
                CartonType = entry.CartonType,
                Statement = entry.Statement,
                Cartons = entry.Cartons > 0 ? entry.Cartons : entry.Outgoing,
                PricePerCarton = entry.PricePerCarton,
                Amount = entry.Amount > 0 ? entry.Amount : entry.Cartons * entry.PricePerCarton
            })
            .ToListAsync();

        var paidQuery = _context.StripInventoryEntries
            .AsNoTracking()
            .Where(entry => entry.StripCustomerId != null && entry.EntryType == StripEntryType.Payment);
        if (fromDate.HasValue)
            paidQuery = paidQuery.Where(entry => entry.Date >= fromDate.Value.Date);
        if (toDate.HasValue)
            paidQuery = paidQuery.Where(entry => entry.Date <= toDate.Value.Date);
        if (customerId.HasValue)
            paidQuery = paidQuery.Where(entry => entry.StripCustomerId == customerId.Value);

        var paidByCustomer = await paidQuery
            .GroupBy(entry => entry.StripCustomerId!.Value)
            .Select(group => new { CustomerId = group.Key, Paid = group.Sum(entry => entry.PaidAmount) })
            .ToDictionaryAsync(item => item.CustomerId, item => item.Paid);

        var summaries = rows
            .GroupBy(row => new { row.CustomerId, row.CustomerName })
            .Select(group => new StripSalesCustomerSummaryViewModel
            {
                CustomerId = group.Key.CustomerId,
                CustomerName = group.Key.CustomerName,
                FiveKilogramCartons = group
                    .Where(row => row.CartonType == StripCartonType.FiveKilogram)
                    .Sum(row => row.Cartons),
                NineHundredGramCartons = group
                    .Where(row => row.CartonType == StripCartonType.NineHundredGram)
                    .Sum(row => row.Cartons),
                TotalSales = group.Sum(row => row.Amount),
                TotalPaid = paidByCustomer.GetValueOrDefault(group.Key.CustomerId)
            })
            .OrderBy(summary => summary.CustomerName)
            .ToList();

        ViewBag.Customers = await _context.StripCustomers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ToListAsync();

        return View(new StripSalesReportViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            CustomerId = customerId,
            Rows = rows,
            Summaries = summaries
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
                    .Where(entry => entry.CartonType == StripCartonType.FiveKilogram && entry.StripCustomerId != null)
                    .Sum(entry => (int?)(entry.Outgoing - entry.Incoming)) ?? 0,
                NineHundredGramBalance = customer.Entries
                    .Where(entry => entry.CartonType == StripCartonType.NineHundredGram && entry.StripCustomerId != null)
                    .Sum(entry => (int?)(entry.Outgoing - entry.Incoming)) ?? 0,
                TotalSales = customer.Entries
                    .Where(entry => entry.StripCustomerId != null)
                    .Sum(entry => (decimal?)(entry.Amount > 0 ? entry.Amount : entry.Cartons * entry.PricePerCarton)) ?? 0,
                TotalPaid = customer.Entries
                    .Where(entry => entry.StripCustomerId != null)
                    .Sum(entry => (decimal?)entry.PaidAmount) ?? 0,
                EntryCount = customer.Entries.Count(entry => entry.StripCustomerId != null),
                LastEntryDate = customer.Entries
                    .Where(entry => entry.StripCustomerId != null)
                    .OrderByDescending(entry => entry.Date)
                    .ThenByDescending(entry => entry.Id)
                    .Select(entry => (DateTime?)entry.Date)
                    .FirstOrDefault()
            })
            .ToListAsync();

        foreach (var customer in customers)
            customer.BalanceDue = customer.TotalSales - customer.TotalPaid;

        var allCustomerEntries = await _context.StripInventoryEntries
            .AsNoTracking()
            .Where(entry => entry.StripCustomerId != null)
            .Select(entry => new
            {
                Amount = entry.Amount > 0 ? entry.Amount : entry.Cartons * entry.PricePerCarton,
                entry.PaidAmount
            })
            .ToListAsync();

        return View(new StripCustomersViewModel
        {
            SearchTerm = searchTerm,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalSales = allCustomerEntries.Sum(entry => entry.Amount),
            TotalPaid = allCustomerEntries.Sum(entry => entry.PaidAmount),
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
    public async Task<IActionResult> AddCustomerEntry(
        [Bind(Prefix = "NewEntry")] StripCustomerEntryInputViewModel entry)
    {
        var customer = await _context.StripCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == entry.CustomerId);
        if (customer is not { } selectedCustomer)
            return NotFound();

        if (!Enum.IsDefined(entry.CartonType))
            ModelState.AddModelError(nameof(entry.CartonType), "اختر نوع كراتين صحيحًا.");
        if (entry.Cartons <= 0)
            ModelState.AddModelError(nameof(entry.Cartons), "أدخل عدد الكراتين.");
        if (entry.PricePerCarton <= 0)
            ModelState.AddModelError(nameof(entry.PricePerCarton), "أدخل سعر الكرتونة.");

        if (Enum.IsDefined(entry.CartonType) && entry.Cartons > 0)
        {
            var stockBalance = await GetStockBalanceAtDateAsync(entry.CartonType, entry.Date);
            if (entry.Cartons > stockBalance)
                ModelState.AddModelError(nameof(entry.Cartons), $"المتاح في المخزن حتى هذا التاريخ {stockBalance} كرتونة فقط.");
        }

        if (!ModelState.IsValid)
            return View("CustomerProfile", await BuildCustomerProfileAsync(entry.CustomerId, entry));

        var amount = entry.Cartons * entry.PricePerCarton;
        var stripEntry = new StripInventoryEntry
        {
            StripCustomerId = selectedCustomer.Id,
            CartonType = entry.CartonType,
            EntryType = StripEntryType.Sale,
            ReceivedBy = selectedCustomer.Name,
            Statement = string.IsNullOrWhiteSpace(entry.Statement) ? "بيع كراتين" : entry.Statement.Trim(),
            Incoming = 0,
            Outgoing = entry.Cartons,
            Cartons = entry.Cartons,
            PricePerCarton = entry.PricePerCarton,
            Amount = amount,
            PaidAmount = 0,
            Date = entry.Date.Date
        };
        _context.StripInventoryEntries.Add(stripEntry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تسجيل البيع وتحديث المخزن ورصيد العميل.";

        return RedirectToAction(nameof(CustomerProfile), new { id = entry.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomerPayment(
        [Bind(Prefix = "NewPayment")] StripCustomerPaymentInputViewModel payment)
    {
        var customer = await _context.StripCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payment.CustomerId);
        if (customer is not { })
            return NotFound();

        if (!ModelState.IsValid)
            return View("CustomerProfile", await BuildCustomerProfileAsync(payment.CustomerId, null, payment));
        if (!IsFridgeCollectionAccount(payment.TreasuryAccount))
        {
            ModelState.AddModelError(
                "NewPayment.TreasuryAccount",
                "اختر خزنة تحصيل صحيحة.");
            return View("CustomerProfile", await BuildCustomerProfileAsync(payment.CustomerId, null, payment));
        }

        var paymentEntry = new StripInventoryEntry
        {
            StripCustomerId = customer.Id,
            CartonType = StripCartonType.FiveKilogram,
            EntryType = StripEntryType.Payment,
            ReceivedBy = customer.Name,
            Statement = string.IsNullOrWhiteSpace(payment.Statement) ? "دفعة" : payment.Statement.Trim(),
            Incoming = 0,
            Outgoing = 0,
            Cartons = 0,
            PricePerCarton = 0,
            Amount = 0,
            PaidAmount = payment.PaidAmount,
            Date = payment.Date.Date
        };
        _context.StripInventoryEntries.Add(paymentEntry);
        await _context.SaveChangesAsync();
        await SyncFridgePaymentAsync(paymentEntry, customer, payment.TreasuryAccount);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"تم تسجيل الدفعة بنجاح، وظهرت في التلاجة ودخلت {TreasuryEntry.GetAccountName(payment.TreasuryAccount)}.";

        return RedirectToAction(nameof(CustomerProfile), new { id = payment.CustomerId });
    }

    public async Task<IActionResult> EditCustomerEntry(int id)
    {
        var entry = await _context.StripInventoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.StripCustomerId != null);
        if (entry == null)
            return NotFound();

        var treasuryAccount = await _context.FridgeEntries
            .Where(fridgeEntry => fridgeEntry.StripInventoryEntryId == entry.Id)
            .Select(fridgeEntry => fridgeEntry.TreasuryEntry == null
                ? (TreasuryAccount?)null
                : fridgeEntry.TreasuryEntry.Account)
            .FirstOrDefaultAsync();

        return View(new StripCustomerEntryInputViewModel
        {
            Id = entry.Id,
            CustomerId = entry.StripCustomerId!.Value,
            CartonType = entry.CartonType,
            EntryType = entry.EntryType,
            Statement = entry.Statement,
            Cartons = entry.Cartons > 0 ? entry.Cartons : entry.Outgoing,
            PricePerCarton = entry.PricePerCarton,
            PaidAmount = entry.PaidAmount,
            TreasuryAccount = treasuryAccount ?? TreasuryAccount.AbuMazenCash,
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

        if (entry.EntryType == StripEntryType.Payment)
        {
            if (model.PaidAmount <= 0)
                ModelState.AddModelError(nameof(model.PaidAmount), "أدخل مبلغ الدفعة.");
            if (!IsFridgeCollectionAccount(model.TreasuryAccount))
                ModelState.AddModelError(nameof(model.TreasuryAccount), "اختر خزنة تحصيل صحيحة.");

            if (!ModelState.IsValid)
                return View(model);

            entry.PaidAmount = model.PaidAmount;
            entry.Date = model.Date.Date;
            await _context.SaveChangesAsync();

            var paymentCustomer = await _context.StripCustomers
                .AsNoTracking()
                .FirstAsync(item => item.Id == model.CustomerId);
            await SyncFridgePaymentAsync(entry, paymentCustomer, model.TreasuryAccount);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم تعديل الدفعة بنجاح، وتم تحديث التلاجة و{TreasuryEntry.GetAccountName(model.TreasuryAccount)}.";
            return RedirectToAction(nameof(CustomerProfile), new { id = model.CustomerId });
        }

        if (!Enum.IsDefined(model.CartonType))
            ModelState.AddModelError(nameof(model.CartonType), "اختر نوع كراتين صحيحًا.");
        if (model.Cartons <= 0)
            ModelState.AddModelError(nameof(model.Cartons), "أدخل عدد الكراتين.");
        if (model.PricePerCarton <= 0)
            ModelState.AddModelError(nameof(model.PricePerCarton), "أدخل سعر الكرتونة.");

        if (Enum.IsDefined(model.CartonType) && model.Cartons > 0)
        {
            var stockBalance = await GetStockBalanceAtDateAsync(model.CartonType, model.Date, entry.Id);
            if (model.Cartons > stockBalance)
                ModelState.AddModelError(nameof(model.Cartons), $"المتاح في المخزن حتى هذا التاريخ {stockBalance} كرتونة فقط.");
        }

        if (!ModelState.IsValid)
            return View(model);

        entry.CartonType = model.CartonType;
        entry.Statement = string.IsNullOrWhiteSpace(model.Statement) ? "بيع كراتين" : model.Statement.Trim();
        entry.Cartons = model.Cartons;
        entry.PricePerCarton = model.PricePerCarton;
        entry.Amount = model.Cartons * model.PricePerCarton;
        entry.Outgoing = model.Cartons;
        entry.Incoming = 0;
        entry.Date = model.Date.Date;
        await _context.SaveChangesAsync();

        await RemoveFridgeSyncAsync(entry);
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
        await RemoveFridgeSyncAsync(entry);
        _context.StripInventoryEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حذف الحركة وتحديث رصيد المخزن والعميل والتلاجة.";

        return RedirectToAction(nameof(CustomerProfile), new { id = customerId });
    }

    public async Task<IActionResult> Details(StripCartonType cartonType)
    {
        if (!Enum.IsDefined(cartonType))
            return NotFound();

        return View(await BuildPageAsync(cartonType));
    }

    public async Task<IActionResult> EditEntry(int id)
    {
        var entry = await _context.StripInventoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.StripCustomerId == null);
        if (entry == null)
            return NotFound();

        return View(new StripInventoryEntryInputViewModel
        {
            Id = entry.Id,
            CartonType = entry.CartonType,
            ReceivedBy = entry.ReceivedBy,
            Statement = entry.Statement,
            Incoming = entry.Incoming,
            Outgoing = entry.Outgoing,
            Date = entry.Date
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEntry(StripInventoryEntryInputViewModel model)
    {
        var entry = await _context.StripInventoryEntries
            .FirstOrDefaultAsync(item => item.Id == model.Id && item.StripCustomerId == null);
        if (entry == null)
            return NotFound();

        if (!Enum.IsDefined(model.CartonType))
            ModelState.AddModelError(nameof(model.CartonType), "اختر نوع كراتين صحيحًا.");
        if (model.Incoming == 0 && model.Outgoing == 0)
            ModelState.AddModelError(string.Empty, "أدخل عدد الكراتين الواردة أو المسحوبة.");
        if (model.Incoming > 0 && model.Outgoing > 0)
            ModelState.AddModelError(string.Empty, "اختر الوارد أو المسحوب في الحركة الواحدة.");

        if (Enum.IsDefined(model.CartonType) && model.Outgoing > 0)
        {
            var availableBalance = await _context.StripInventoryEntries
                .Where(item =>
                    item.Id != entry.Id &&
                    item.CartonType == model.CartonType &&
                    item.Date <= model.Date.Date)
                .SumAsync(item => item.Incoming - item.Outgoing);

            if (model.Outgoing > availableBalance)
                ModelState.AddModelError(
                    nameof(model.Outgoing),
                    $"الرصيد المتاح حتى التاريخ المحدد {availableBalance} كرتونة فقط.");
        }

        if (!ModelState.IsValid)
            return View(model);

        entry.CartonType = model.CartonType;
        entry.EntryType = model.Incoming > 0 ? StripEntryType.StockIn : StripEntryType.StockOut;
        entry.ReceivedBy = model.ReceivedBy.Trim();
        entry.Statement = model.Statement.Trim();
        entry.Incoming = model.Incoming;
        entry.Outgoing = model.Outgoing;
        entry.Date = model.Date.Date;

        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تعديل الحركة وتحديث رصيد المخزن.";
        return RedirectToAction(nameof(Details), new { cartonType = model.CartonType });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(int id)
    {
        var entry = await _context.StripInventoryEntries
            .FirstOrDefaultAsync(item => item.Id == id && item.StripCustomerId == null);
        if (entry == null)
            return NotFound();

        var cartonType = entry.CartonType;
        _context.StripInventoryEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حذف الحركة وتحديث رصيد المخزن.";

        return RedirectToAction(nameof(Details), new { cartonType });
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
            EntryType = entry.Incoming > 0 ? StripEntryType.StockIn : StripEntryType.StockOut,
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

    // ================= التلاجة =================

    public async Task<IActionResult> Fridge()
    {
        var page = await BuildFridgePageAsync();
        if (page == null)
            return NotFound();

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFridgeEntry(
        [Bind(Prefix = "NewEntry")] FridgeEntryInputViewModel entry)
    {
        var fridgeClient = await GetFridgeClientAsync();
        if (fridgeClient == null)
            return NotFound();

        var incoming = entry.Incoming ?? 0;
        var outgoing = entry.Outgoing ?? 0;
        if (incoming <= 0 && outgoing <= 0)
            ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو الصادر.");
        if (incoming > 0 && outgoing > 0)
            ModelState.AddModelError(string.Empty, "اختر الوارد أو الصادر في الحركة الواحدة.");
        if (incoming > 0 && !IsFridgeCollectionAccount(entry.TreasuryAccount))
            ModelState.AddModelError("NewEntry.TreasuryAccount", "اختر خزنة أبو مازن النقدي أو فودافون كاش أو إنستا باي.");

        if (!ModelState.IsValid)
        {
            var page = await BuildFridgePageAsync(entry);
            if (page == null)
                return NotFound();
            return View(nameof(Fridge), page);
        }

        var statement = entry.Statement.Trim();
        var fridgeEntry = new FridgeEntry
        {
            ClientId = fridgeClient.Id,
            Date = entry.Date.Date,
            Statement = statement,
            Incoming = incoming,
            Outgoing = outgoing,
            Source = FridgeEntrySource.Manual
        };

        if (incoming > 0)
        {
            var treasuryEntry = new TreasuryEntry
            {
                Account = entry.TreasuryAccount,
                Statement = Limit($"وارد التلاجة: {statement}"),
                Incoming = incoming,
                Outgoing = 0,
                Date = entry.Date.Date
            };
            _context.TreasuryEntries.Add(treasuryEntry);
            fridgeEntry.TreasuryEntry = treasuryEntry;
        }

        _context.FridgeEntries.Add(fridgeEntry);
        await _context.SaveChangesAsync();

        TempData["Success"] = incoming > 0
            ? $"تم تسجيل الوارد في التلاجة وإضافته إلى {TreasuryEntry.GetAccountName(entry.TreasuryAccount)}."
            : "تم تسجيل الصادر في التلاجة.";

        return RedirectToAction(nameof(Fridge));
    }

    public async Task<IActionResult> EditFridgeEntry(int id)
    {
        var entry = await _context.FridgeEntries
            .AsNoTracking()
            .Include(item => item.TreasuryEntry)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (entry == null)
            return NotFound();

        if (entry.Source != FridgeEntrySource.Manual)
        {
            TempData["Error"] = "هذه الحركة مرتبطة بعملاء الاستربس؛ عدّلها من حساب العميل نفسه.";
            return RedirectToAction(nameof(Fridge));
        }

        return View(new FridgeEntryInputViewModel
        {
            Id = entry.Id,
            Date = entry.Date,
            Statement = entry.Statement,
            Incoming = entry.Incoming > 0 ? entry.Incoming : null,
            Outgoing = entry.Outgoing > 0 ? entry.Outgoing : null,
            TreasuryAccount = entry.TreasuryEntry?.Account ?? TreasuryAccount.AbuMazenCash
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditFridgeEntry(FridgeEntryInputViewModel model)
    {
        var entry = await _context.FridgeEntries
            .Include(item => item.TreasuryEntry)
            .FirstOrDefaultAsync(item => item.Id == model.Id);
        if (entry == null)
            return NotFound();

        if (entry.Source != FridgeEntrySource.Manual)
        {
            TempData["Error"] = "هذه الحركة مرتبطة بعملاء الاستربس؛ عدّلها من حساب العميل نفسه.";
            return RedirectToAction(nameof(Fridge));
        }

        var incoming = model.Incoming ?? 0;
        var outgoing = model.Outgoing ?? 0;
        if (incoming <= 0 && outgoing <= 0)
            ModelState.AddModelError(string.Empty, "أدخل مبلغًا في الوارد أو الصادر.");
        if (incoming > 0 && outgoing > 0)
            ModelState.AddModelError(string.Empty, "اختر الوارد أو الصادر في الحركة الواحدة.");
        if (incoming > 0 && !IsFridgeCollectionAccount(model.TreasuryAccount))
            ModelState.AddModelError(nameof(model.TreasuryAccount), "اختر خزنة أبو مازن النقدي أو فودافون كاش أو إنستا باي.");

        if (!ModelState.IsValid)
            return View(model);

        var statement = model.Statement.Trim();
        entry.Date = model.Date.Date;
        entry.Statement = statement;
        entry.Incoming = incoming;
        entry.Outgoing = outgoing;

        if (incoming > 0)
        {
            if (entry.TreasuryEntry == null)
            {
                var treasuryEntry = new TreasuryEntry
                {
                    Account = model.TreasuryAccount,
                    Statement = Limit($"وارد التلاجة: {statement}"),
                    Incoming = incoming,
                    Outgoing = 0,
                    Date = model.Date.Date
                };
                _context.TreasuryEntries.Add(treasuryEntry);
                entry.TreasuryEntry = treasuryEntry;
            }
            else
            {
                entry.TreasuryEntry.Account = model.TreasuryAccount;
                entry.TreasuryEntry.Statement = Limit($"وارد التلاجة: {statement}");
                entry.TreasuryEntry.Incoming = incoming;
                entry.TreasuryEntry.Outgoing = 0;
                entry.TreasuryEntry.Date = model.Date.Date;
            }
        }
        else if (entry.TreasuryEntry != null)
        {
            _context.TreasuryEntries.Remove(entry.TreasuryEntry);
            entry.TreasuryEntry = null;
            entry.TreasuryEntryId = null;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تعديل حركة التلاجة.";

        return RedirectToAction(nameof(Fridge));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFridgeEntry(int id)
    {
        var entry = await _context.FridgeEntries
            .Include(item => item.TreasuryEntry)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (entry == null)
            return NotFound();

        if (entry.Source != FridgeEntrySource.Manual)
        {
            TempData["Error"] = "هذه الحركة مرتبطة بعملاء الاستربس؛ احذفها من حساب العميل نفسه.";
            return RedirectToAction(nameof(Fridge));
        }

        if (entry.TreasuryEntry != null)
        {
            _context.TreasuryEntries.Remove(entry.TreasuryEntry);
            entry.TreasuryEntry = null;
            entry.TreasuryEntryId = null;
        }

        _context.FridgeEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حذف حركة التلاجة.";

        return RedirectToAction(nameof(Fridge));
    }

    private async Task<FridgePageViewModel?> BuildFridgePageAsync(FridgeEntryInputViewModel? newEntry = null)
    {
        var fridgeClient = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(client => client.IsFridge);
        if (fridgeClient == null)
            return null;

        var entries = await _context.FridgeEntries
            .AsNoTracking()
            .Include(entry => entry.StripCustomer)
            .Include(entry => entry.TreasuryEntry)
            .Where(entry => entry.ClientId == fridgeClient.Id && entry.Source != FridgeEntrySource.StripSale)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Id)
            .ToListAsync();

        var page = new FridgePageViewModel
        {
            ClientId = fridgeClient.Id,
            NewEntry = newEntry ?? new FridgeEntryInputViewModel { Date = DateTime.Today },
            TotalOutgoing = entries.Sum(entry => entry.Outgoing),
            TotalIncoming = entries.Sum(entry => entry.Incoming),
            StripCustomerCount = await _context.StripCustomers.CountAsync(),
            StripPaidTotal = await _context.StripInventoryEntries
                .Where(entry => entry.StripCustomerId != null && entry.EntryType == StripEntryType.Payment)
                .SumAsync(entry => entry.PaidAmount)
        };

        decimal balance = 0;
        foreach (var entry in entries)
        {
            balance += entry.Outgoing - entry.Incoming;
            page.Entries.Add(new FridgeEntryRowViewModel
            {
                Id = entry.Id,
                Date = entry.Date,
                Statement = entry.Statement,
                Incoming = entry.Incoming,
                Outgoing = entry.Outgoing,
                Balance = balance,
                Source = entry.Source,
                StripCustomerId = entry.StripCustomerId,
                StripCustomerName = entry.StripCustomer?.Name,
                HasTreasuryEntry = entry.TreasuryEntryId.HasValue,
                TreasuryAccountName = entry.TreasuryEntry == null
                    ? null
                    : TreasuryEntry.GetAccountName(entry.TreasuryEntry.Account)
            });
        }

        page.Entries.Reverse();
        return page;
    }

    private async Task<Client?> GetFridgeClientAsync() =>
        await _context.Clients.FirstOrDefaultAsync(client => client.IsFridge);

    private async Task SyncFridgePaymentAsync(
        StripInventoryEntry entry,
        StripCustomer customer,
        TreasuryAccount treasuryAccount)
    {
        var fridgeClient = await GetFridgeClientAsync();
        if (fridgeClient == null)
            return;

        var statement = Limit($"تحصيل من عميل الاستربس {customer.Name}: {entry.Statement}");
        var fridgeEntry = await _context.FridgeEntries
            .Include(item => item.TreasuryEntry)
            .FirstOrDefaultAsync(item => item.StripInventoryEntryId == entry.Id);

        if (fridgeEntry == null)
        {
            var treasuryEntry = new TreasuryEntry
            {
                Account = treasuryAccount,
                Statement = statement,
                Incoming = entry.PaidAmount,
                Outgoing = 0,
                Date = entry.Date
            };
            _context.TreasuryEntries.Add(treasuryEntry);
            _context.FridgeEntries.Add(new FridgeEntry
            {
                ClientId = fridgeClient.Id,
                Date = entry.Date,
                Statement = statement,
                Incoming = entry.PaidAmount,
                Outgoing = 0,
                Source = FridgeEntrySource.StripPayment,
                StripInventoryEntryId = entry.Id,
                StripCustomerId = customer.Id,
                TreasuryEntry = treasuryEntry
            });
            return;
        }

        fridgeEntry.ClientId = fridgeClient.Id;
        fridgeEntry.Date = entry.Date;
        fridgeEntry.Statement = statement;
        fridgeEntry.Incoming = entry.PaidAmount;
        fridgeEntry.Outgoing = 0;
        fridgeEntry.StripCustomerId = customer.Id;

        if (fridgeEntry.TreasuryEntry == null)
        {
            var treasuryEntry = new TreasuryEntry
            {
                Account = treasuryAccount,
                Statement = statement,
                Incoming = entry.PaidAmount,
                Outgoing = 0,
                Date = entry.Date
            };
            _context.TreasuryEntries.Add(treasuryEntry);
            fridgeEntry.TreasuryEntry = treasuryEntry;
            return;
        }

        fridgeEntry.TreasuryEntry.Account = treasuryAccount;
        fridgeEntry.TreasuryEntry.Statement = statement;
        fridgeEntry.TreasuryEntry.Incoming = entry.PaidAmount;
        fridgeEntry.TreasuryEntry.Outgoing = 0;
        fridgeEntry.TreasuryEntry.Date = entry.Date;
    }

    private async Task RemoveFridgeSyncAsync(StripInventoryEntry entry)
    {
        var fridgeEntry = await _context.FridgeEntries
            .Include(item => item.TreasuryEntry)
            .FirstOrDefaultAsync(item => item.StripInventoryEntryId == entry.Id);
        if (fridgeEntry == null)
            return;

        if (fridgeEntry.TreasuryEntry != null)
        {
            _context.TreasuryEntries.Remove(fridgeEntry.TreasuryEntry);
            fridgeEntry.TreasuryEntry = null;
            fridgeEntry.TreasuryEntryId = null;
        }

        _context.FridgeEntries.Remove(fridgeEntry);
    }

    private static string Limit(string value, int maxLength = 500) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static bool IsFridgeCollectionAccount(TreasuryAccount account) =>
        account is TreasuryAccount.AbuMazenCash
            or TreasuryAccount.AbuMazenVodafoneCash
            or TreasuryAccount.AbuMazenInstaPay
            or TreasuryAccount.MohamedAbdelrahmanCash
            or TreasuryAccount.MohamedAbdelrahmanVodafoneCash
            or TreasuryAccount.MohamedAbdelrahmanInstaPay;

    private async Task<StripInventoryPageViewModel> BuildPageAsync(
        StripCartonType cartonType,
        StripInventoryEntryInputViewModel? newEntry = null)
    {
        var allEntries = await _context.StripInventoryEntries
            .AsNoTracking()
            .Include(entry => entry.Customer)
            .Where(entry => entry.EntryType != StripEntryType.Payment && (entry.Incoming != 0 || entry.Outgoing != 0))
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

        page.Entries.Reverse();
        return page;
    }

    private async Task<StripCustomerProfileViewModel> BuildCustomerProfileAsync(
        int customerId,
        StripCustomerEntryInputViewModel? newEntry = null,
        StripCustomerPaymentInputViewModel? newPayment = null)
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

        var stockBalances = new Dictionary<StripCartonType, int>
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
                Date = DateTime.Today,
                Statement = customer.Name
            },
            NewPayment = newPayment ?? new StripCustomerPaymentInputViewModel
            {
                CustomerId = customerId,
                Date = DateTime.Today,
                Statement = "دفعة"
            }
        };

        decimal runningDue = 0;
        foreach (var entry in entries)
        {
            if (entry.StripCustomerId == null) continue;
            if (entry.EntryType == StripEntryType.Sale || entry.EntryType == StripEntryType.Return || entry.Outgoing > 0 || entry.Incoming > 0)
            {
                stockBalances[entry.CartonType] += entry.Outgoing - entry.Incoming;
            }

            var saleAmount = entry.Amount > 0 ? entry.Amount : entry.Cartons * entry.PricePerCarton;
            runningDue += saleAmount - entry.PaidAmount;

            page.Entries.Add(new StripCustomerEntryViewModel
            {
                Id = entry.Id,
                CartonType = entry.CartonType,
                EntryType = entry.EntryType,
                Date = entry.Date,
                Statement = entry.Statement,
                Cartons = entry.Cartons,
                PricePerCarton = entry.PricePerCarton,
                Amount = saleAmount,
                PaidAmount = entry.PaidAmount,
                Incoming = entry.Incoming,
                Outgoing = entry.Outgoing,
                Balance = stockBalances[entry.CartonType],
                RunningDue = runningDue
            });
        }

        page.FiveKilogramBalance = stockBalances[StripCartonType.FiveKilogram];
        page.NineHundredGramBalance = stockBalances[StripCartonType.NineHundredGram];
        page.TotalSales = entries.Where(entry => entry.StripCustomerId != null).Sum(entry => entry.Amount > 0 ? entry.Amount : entry.Cartons * entry.PricePerCarton);
        page.TotalPaid = entries.Where(entry => entry.StripCustomerId != null).Sum(entry => entry.PaidAmount);
        page.Entries.Reverse();
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
