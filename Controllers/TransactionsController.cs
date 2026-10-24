using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.Services;
using HagImamAccounts.ViewModels;
using System.Globalization;

namespace HagImamAccounts.Controllers
{
    public class TransactionsController : Controller
    {
        private const string BulkMealSavePassword = "123";
        private readonly ApplicationDbContext _context;

        public TransactionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Transactions/Create/5 (ClientId)
        public async Task<IActionResult> Create(int? clientId, string section = "accounts")
        {
            if (clientId == null)
                return NotFound();

            var client = await _context.Clients.FindAsync(clientId);
            if (client == null)
                return NotFound();

            var transaction = new Transaction
            {
                ClientId = clientId.Value,
                Date = DateTime.Now,
                GrossWeightExpression = "0",
                EmptyWeightExpression = "0"
            };

            // Carry forward the client's overall balance shown on the profile.
            var transactions = await _context.Transactions
                .Where(t => t.ClientId == clientId && !t.IsDeleted)
                .ToListAsync();
            transaction.OldBalance = transactions.Sum(t => t.TotalDue);

            ViewBag.ClientName = client.Name;
            ViewBag.ActiveStep = section == "meal" ? "meal" : "accounts";
            return View(transaction);
        }

        // POST: Transactions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Transaction transaction)
        {
            // Remove navigation property from validation
            ModelState.Remove("Client");
            TryParseMealWeightExpressions(transaction);

            if (ModelState.IsValid)
            {
                transaction.PricePerKilo = transaction.FarmPrice + transaction.ExtraField;
                transaction.MealType = NormalizeMealType(transaction.MealType);
                transaction.IsDeleted = false;
                _context.Add(transaction);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة الوجبة بنجاح";
                return RedirectToAction("Profile", "Clients", new { id = transaction.ClientId });
            }

            var client = await _context.Clients.FindAsync(transaction.ClientId);
            ViewBag.ClientName = client?.Name ?? "";
            ViewBag.ActiveStep = "accounts";
            return View(transaction);
        }

        // POST: Transactions/CreateCollectionOnly
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCollectionOnly(Transaction transaction)
        {
            ModelState.Remove("Client");

            transaction.GrossWeight = 0;
            transaction.EmptyWeight = 0;
            transaction.FarmPrice = 0;
            transaction.ExtraField = 0;
            transaction.PricePerKilo = 0;
            transaction.GeneralNotes = null;

            if (ModelState.IsValid)
            {
                transaction.IsDeleted = false;
                _context.Add(transaction);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حفظ التحصيل بنجاح";
                return RedirectToAction("Profile", "Clients", new { id = transaction.ClientId });
            }

            var client = await _context.Clients.FindAsync(transaction.ClientId);
            ViewBag.ClientName = client?.Name ?? "";
            ViewBag.ActiveStep = "accounts";
            return View("Create", transaction);
        }

        // POST: Transactions/CreateMealOnly
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMealOnly(Transaction transaction)
        {
            ModelState.Remove("Client");
            TryParseMealWeightExpressions(transaction);
            var previousTransactions = await _context.Transactions
                .Where(t => t.ClientId == transaction.ClientId && !t.IsDeleted)
                .ToListAsync();
            transaction.OldBalance = previousTransactions.Sum(t => t.TotalDue);
            transaction.CashCollection = 0;
            transaction.ElectronicCollection = 0;
            transaction.AdditionAmount = 0;
            transaction.AdditionNotes = null;
            transaction.DiscountAmount = 0;
            transaction.DiscountNotes = null;

            if (ModelState.IsValid)
            {
                transaction.PricePerKilo = transaction.FarmPrice + transaction.ExtraField;
                transaction.MealType = NormalizeMealType(transaction.MealType);
                transaction.IsDeleted = false;
                _context.Add(transaction);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة الوجبة بنجاح";
                return RedirectToAction("Profile", "Clients", new { id = transaction.ClientId });
            }

            var client = await _context.Clients.FindAsync(transaction.ClientId);
            ViewBag.ClientName = client?.Name ?? "";
            ViewBag.ActiveStep = "meal";
            return View("Create", transaction);
        }

        // GET: Transactions/BulkMeal
        public async Task<IActionResult> BulkMeal()
        {
            ViewBag.BulkMealSaved = TempData["BulkMealSaved"] is true;
            return View(await BuildBulkMealViewModelAsync());
        }

        // POST: Transactions/BulkMeal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkMeal(BulkMealViewModel model, string? savePassword)
        {
            model.MealType = NormalizeMealType(model.MealType);
            if (savePassword != BulkMealSavePassword)
            {
                ModelState.AddModelError(string.Empty, "كلمة المرور غير صحيحة. أدخل كلمة المرور لحفظ الوجبات.");
                return View(await BuildBulkMealViewModelAsync(model));
            }

            if (!DateTime.TryParseExact(
                    model.DateText,
                    new[] { "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var mealDate))
            {
                ModelState.AddModelError(nameof(model.DateText), "أدخل التاريخ بصيغة يوم/شهر/سنة، مثال: 20/10/2026.");
            }
            else
            {
                model.Date = mealDate;
            }

            if (model.FarmPrice < 0)
                ModelState.AddModelError(nameof(model.FarmPrice), "سعر المزرعة لا يمكن أن يكون سالبًا.");
            if (model.FarmWeight < 0)
                ModelState.AddModelError(nameof(model.FarmWeight), "وزن المزرعة لا يمكن أن يكون سالبًا.");
            if (model.FarmWeight > 0 && string.IsNullOrWhiteSpace(model.FarmName))
                ModelState.AddModelError(nameof(model.FarmName), "اسم المزرعة مطلوب عند إدخال وزنها.");

            var clients = await _context.Clients
                .Where(client => client.IsActive)
                .OrderBy(client => client.Name)
                .ToListAsync();
            var clientById = clients.ToDictionary(client => client.Id);
            var clientIds = clients.Select(client => client.Id).ToList();
            var histories = await _context.Transactions
                .Where(transaction => !transaction.IsDeleted && clientIds.Contains(transaction.ClientId))
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.Id)
                .ToListAsync();
            var historyByClient = histories.GroupBy(transaction => transaction.ClientId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var entries = new List<(Client Client, BulkMealRowViewModel Row, decimal GrossWeight, decimal EmptyWeight)>();

            for (var index = 0; index < model.Rows.Count; index++)
            {
                var row = model.Rows[index];
                if (!clientById.TryGetValue(row.ClientId, out var client))
                {
                    ModelState.AddModelError(string.Empty, "قائمة العملاء تغيرت. حدّث الصفحة وحاول مرة أخرى.");
                    continue;
                }

                var hasInput = !string.IsNullOrWhiteSpace(row.GrossWeightExpression) ||
                               !string.IsNullOrWhiteSpace(row.EmptyWeightExpression) ||
                               !string.IsNullOrWhiteSpace(row.Notes) ||
                               row.PriceIncrement != 0;
                if (!hasInput)
                    continue;

                var grossIsValid = ArithmeticExpressionParser.TryParse(row.GrossWeightExpression, out var grossWeight);
                var emptyIsValid = string.IsNullOrWhiteSpace(row.EmptyWeightExpression) ||
                                   ArithmeticExpressionParser.TryParse(row.EmptyWeightExpression, out _);
                var emptyWeight = 0m;
                if (emptyIsValid && !string.IsNullOrWhiteSpace(row.EmptyWeightExpression))
                    ArithmeticExpressionParser.TryParse(row.EmptyWeightExpression, out emptyWeight);

                if (!grossIsValid)
                    ModelState.AddModelError($"Rows[{index}].GrossWeightExpression", "اكتب رقمًا أو عملية جمع صحيحة.");
                if (!emptyIsValid)
                    ModelState.AddModelError($"Rows[{index}].EmptyWeightExpression", "اكتب رقمًا أو عملية ضرب صحيحة.");
                if (grossIsValid && grossWeight < 0 || emptyIsValid && emptyWeight < 0)
                    ModelState.AddModelError(string.Empty, $"الأوزان لا يمكن أن تكون سالبة للعميل {client.Name}.");

                if (grossIsValid && emptyIsValid)
                    entries.Add((client, row, grossWeight, emptyWeight));
            }

            if (!ModelState.IsValid)
                return View(await BuildBulkMealViewModelAsync(model));

            var netWeightTotal = entries.Sum(entry => entry.GrossWeight - entry.EmptyWeight);
            var mealTotal = entries.Sum(entry =>
                (entry.GrossWeight - entry.EmptyWeight) * (model.FarmPrice + entry.Row.PriceIncrement));
            FarmMealBatch? farmBatch = null;
            if (entries.Count > 0)
            {
                var farmName = model.FarmName?.Trim() ?? string.Empty;
                var previousFarmBalance = string.IsNullOrEmpty(farmName)
                    ? 0
                    : await _context.FarmMealBatches
                        .AsNoTracking()
                        .Where(batch => batch.FarmName == farmName)
                        .Select(batch => batch.FarmPrice * batch.FarmWeight - batch.FarmPaidAmount)
                        .SumAsync();

                farmBatch = new FarmMealBatch
                {
                    Date = model.Date.Date,
                    FarmName = farmName,
                    FarmWeight = model.FarmWeight,
                    FarmPrice = model.FarmPrice,
                    PreviousFarmBalance = previousFarmBalance,
                    NetWeight = netWeightTotal,
                    MealTotal = mealTotal
                };
                _context.FarmMealBatches.Add(farmBatch);
            }

            foreach (var entry in entries)
            {
                var history = historyByClient.GetValueOrDefault(entry.Client.Id) ?? new List<Transaction>();
                _context.Transactions.Add(new Transaction
                {
                    ClientId = entry.Client.Id,
                    FarmMealBatch = farmBatch,
                    Date = model.Date.Date,
                    OldBalance = history.Sum(transaction => transaction.TotalDue),
                    GrossWeight = entry.GrossWeight,
                    EmptyWeight = entry.EmptyWeight,
                    FarmPrice = model.FarmPrice,
                    ExtraField = entry.Row.PriceIncrement,
                    PricePerKilo = model.FarmPrice + entry.Row.PriceIncrement,
                    MealType = NormalizeMealType(model.MealType),
                    GeneralNotes = string.IsNullOrWhiteSpace(entry.Row.Notes)
                        ? null
                        : entry.Row.Notes.Trim(),
                    IsDeleted = false
                });
            }

            if (entries.Count > 0)
            {
                var profit = farmBatch!.Profit;
                if (profit > 0)
                {
                    _context.ExpenseEntries.Add(new ExpenseEntry
                    {
                        FarmMealBatch = farmBatch,
                        Date = farmBatch.Date,
                        Statement = $"أرباح وجبات - {farmBatch.FarmName}",
                        Incoming = profit
                    });
                }
                else if (profit < 0)
                {
                    _context.ExpenseEntries.Add(new ExpenseEntry
                    {
                        FarmMealBatch = farmBatch,
                        Date = farmBatch.Date,
                        Statement = $"خسارة وجبات - {farmBatch.FarmName}",
                        Outgoing = -profit
                    });
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"تم إضافة الوجبات لعدد {entries.Count} عميل وتسجيل أرباحها في المصروفات.";
                TempData["BulkMealSaved"] = true;
            }
            else
            {
                TempData["Error"] = "أدخل بيانات وجبة لعميل واحد على الأقل.";
            }

            return RedirectToAction(nameof(BulkMeal));
        }

        public async Task<IActionResult> FarmMeals(string? farmName, int page = 1, int pageSize = 10)
        {
            var query = _context.FarmMealBatches
                .AsNoTracking()
                .AsQueryable();

            var normalizedFarmName = farmName?.Trim();
            if (!string.IsNullOrEmpty(normalizedFarmName))
                query = query.Where(batch => batch.FarmName.Contains(normalizedFarmName));

            var totalCount = await query.CountAsync();
            pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Clamp(page, 1, Math.Max(totalPages, 1));

            var batches = await query
                .Include(batch => batch.Transactions)
                .OrderByDescending(batch => batch.Date)
                .ThenByDescending(batch => batch.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(new FarmMealArchiveViewModel
            {
                Batches = batches,
                FarmName = normalizedFarmName,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> EditFarmMeal(int id, int page = 1, int pageSize = 10, string? farmName = null)
        {
            var batch = await _context.FarmMealBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
            if (batch == null)
                return NotFound();

            return View(new FarmMealEditViewModel
            {
                Id = batch.Id,
                Date = batch.Date,
                FarmName = batch.FarmName,
                FarmPrice = batch.FarmPrice,
                FarmWeight = batch.FarmWeight,
                ReturnPage = Math.Max(page, 1),
                ReturnPageSize = pageSize is 10 or 25 or 50 ? pageSize : 10,
                ReturnFarmName = farmName?.Trim()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFarmMeal(FarmMealEditViewModel model)
        {
            model.FarmName = model.FarmName?.Trim() ?? string.Empty;
            model.ReturnPage = Math.Max(model.ReturnPage, 1);
            model.ReturnPageSize = model.ReturnPageSize is 10 or 25 or 50 ? model.ReturnPageSize : 10;
            model.ReturnFarmName = model.ReturnFarmName?.Trim();

            if (!ModelState.IsValid)
                return View(model);

            var batch = await _context.FarmMealBatches
                .Include(item => item.Transactions)
                .FirstOrDefaultAsync(item => item.Id == model.Id);
            if (batch == null)
                return NotFound();

            batch.Date = model.Date.Date;
            batch.FarmName = model.FarmName;
            batch.FarmPrice = model.FarmPrice;
            batch.FarmWeight = model.FarmWeight;

            var activeTransactions = batch.Transactions
                .Where(transaction => !transaction.IsDeleted)
                .ToList();
            foreach (var transaction in batch.Transactions)
            {
                transaction.Date = batch.Date;
                transaction.FarmPrice = batch.FarmPrice;
                transaction.PricePerKilo = batch.FarmPrice + transaction.ExtraField;
            }

            batch.NetWeight = activeTransactions.Sum(transaction => transaction.NetWeight);
            batch.MealTotal = activeTransactions.Sum(transaction => transaction.MealTotal);

            var expense = await _context.ExpenseEntries
                .FirstOrDefaultAsync(entry => entry.FarmMealBatchId == batch.Id);
            var profit = batch.Profit;
            if (profit == 0)
            {
                if (expense != null)
                    _context.ExpenseEntries.Remove(expense);
            }
            else
            {
                expense ??= new ExpenseEntry { FarmMealBatchId = batch.Id };
                expense.Date = batch.Date;
                expense.Statement = profit > 0
                    ? $"أرباح وجبات - {batch.FarmName}"
                    : $"خسارة وجبات - {batch.FarmName}";
                expense.Incoming = Math.Max(profit, 0);
                expense.Outgoing = Math.Max(-profit, 0);
                if (expense.Id == 0)
                    _context.ExpenseEntries.Add(expense);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تعديل بيانات الوجبة بنجاح.";
            return RedirectToAction(nameof(FarmMeals), new
            {
                page = model.ReturnPage,
                pageSize = model.ReturnPageSize,
                farmName = model.ReturnFarmName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFarmMeal(
            int id,
            int page = 1,
            int pageSize = 10,
            string? farmName = null)
        {
            var batch = await _context.FarmMealBatches
                .Include(item => item.Transactions)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (batch == null)
                return NotFound();

            foreach (var transaction in batch.Transactions)
            {
                transaction.IsDeleted = true;
                transaction.FarmMealBatchId = null;
            }

            var expenses = await _context.ExpenseEntries
                .Where(entry => entry.FarmMealBatchId == batch.Id)
                .ToListAsync();
            _context.ExpenseEntries.RemoveRange(expenses);
            _context.FarmMealBatches.Remove(batch);

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حذف الوجبة وتسجيلاتها المرتبطة.";
            return RedirectToAction(nameof(FarmMeals), new
            {
                page = Math.Max(page, 1),
                pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10,
                farmName = farmName?.Trim()
            });
        }

        [HttpGet]
        public async Task<IActionResult> FarmBalance(string farmName)
        {
            if (string.IsNullOrWhiteSpace(farmName))
                return BadRequest();

            var normalizedFarmName = farmName.Trim();
            var balance = await _context.FarmMealBatches
                .AsNoTracking()
                .Where(batch => batch.FarmName == normalizedFarmName)
                .Select(batch => batch.FarmPrice * batch.FarmWeight - batch.FarmPaidAmount)
                .SumAsync();

            return Json(new { balance });
        }

        public async Task<IActionResult> FarmMealDetails(int id)
        {
            var batch = await _context.FarmMealBatches
                .AsNoTracking()
                .Include(item => item.Transactions)
                    .ThenInclude(transaction => transaction.Client)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (batch == null)
                return NotFound();

            batch.PreviousFarmBalance = await _context.FarmMealBatches
                .AsNoTracking()
                .Where(previousBatch =>
                    previousBatch.Id < batch.Id &&
                    previousBatch.FarmName == batch.FarmName)
                .Select(previousBatch =>
                    previousBatch.FarmPrice * previousBatch.FarmWeight - previousBatch.FarmPaidAmount)
                .SumAsync();

            batch.Transactions = batch.Transactions
                .OrderBy(transaction => transaction.Client?.Name)
                .ThenBy(transaction => transaction.Id)
                .ToList();

            return View(batch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFarmPayment(int id, decimal amountPaid)
        {
            if (!ModelState.IsValid || amountPaid < 0)
            {
                TempData["Error"] = "أدخل مبلغًا صحيحًا غير سالب.";
                return RedirectToAction(nameof(FarmMealDetails), new { id });
            }

            var batch = await _context.FarmMealBatches.FindAsync(id);
            if (batch == null)
                return NotFound();

            batch.FarmPaidAmount = amountPaid;
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حفظ المبلغ المدفوع للمزرعة.";
            return RedirectToAction(nameof(FarmMealDetails), new { id });
        }

        private async Task<BulkMealViewModel> BuildBulkMealViewModelAsync(BulkMealViewModel? submitted = null)
        {
            var clients = await _context.Clients
                .AsNoTracking()
                .Where(client => client.IsActive)
                .OrderBy(client => client.Name)
                .ToListAsync();
            var clientIds = clients.Select(client => client.Id).ToList();
            var histories = await _context.Transactions
                .AsNoTracking()
                .Where(transaction => !transaction.IsDeleted && clientIds.Contains(transaction.ClientId))
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.Id)
                .ToListAsync();
            var historyByClient = histories.GroupBy(transaction => transaction.ClientId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var submittedByClient = submitted?.Rows
                .GroupBy(row => row.ClientId)
                .ToDictionary(group => group.Key, group => group.First()) ?? new();
            return new BulkMealViewModel
            {
                Date = submitted?.Date ?? DateTime.Today,
                DateText = submitted?.DateText ?? (submitted?.Date ?? DateTime.Today)
                    .ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                FarmPrice = submitted?.FarmPrice ?? 0,
                MealType = submitted?.MealType,
                FarmName = submitted?.FarmName ?? string.Empty,
                FarmWeight = submitted?.FarmWeight ?? 0,
                Rows = clients.Select(client =>
                {
                    var history = historyByClient.GetValueOrDefault(client.Id) ?? new List<Transaction>();
                    submittedByClient.TryGetValue(client.Id, out var submittedRow);
                    return new BulkMealRowViewModel
                    {
                        ClientId = client.Id,
                        ClientName = client.Name,
                        OldBalance = history.Sum(transaction => transaction.TotalDue),
                        GrossWeightExpression = submittedRow?.GrossWeightExpression ?? string.Empty,
                        EmptyWeightExpression = submittedRow?.EmptyWeightExpression ?? string.Empty,
                        Notes = submittedRow?.Notes,
                        PriceIncrement = submittedRow?.PriceIncrement ?? 0
                    };
                }).ToList()
            };
        }

        // GET: Transactions/Edit/5
        public async Task<IActionResult> Edit(int? id, string section = "accounts")
        {
            if (id == null)
                return NotFound();

            var transaction = await _context.Transactions
                .Include(t => t.Client)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
                return NotFound();

            transaction.GrossWeightExpression = transaction.GrossWeight.ToString(CultureInfo.InvariantCulture);
            transaction.EmptyWeightExpression = transaction.EmptyWeight.ToString(CultureInfo.InvariantCulture);
            ViewBag.ClientName = transaction.Client?.Name ?? "";
            ViewBag.ActiveStep = section == "meal" ? "meal" : "accounts";
            return View(transaction);
        }

        // POST: Transactions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Transaction transaction, string formSection = "accounts")
        {
            if (id != transaction.Id)
                return NotFound();

            // Remove navigation property from validation
            ModelState.Remove("Client");
            if (formSection == "meal")
                TryParseMealWeightExpressions(transaction);

            if (ModelState.IsValid)
            {
                try
                {
                    var existingTransaction = await _context.Transactions.FindAsync(id);
                    if (existingTransaction == null)
                        return NotFound();

                    existingTransaction.Date = transaction.Date;
                    if (formSection == "meal")
                    {
                        existingTransaction.GrossWeight = transaction.GrossWeight;
                        existingTransaction.EmptyWeight = transaction.EmptyWeight;
                        existingTransaction.ExtraField = transaction.ExtraField;
                        existingTransaction.FarmPrice = transaction.FarmPrice;
                        existingTransaction.PricePerKilo = transaction.FarmPrice + transaction.ExtraField;
                        existingTransaction.MealType = NormalizeMealType(transaction.MealType);
                        existingTransaction.GeneralNotes = transaction.GeneralNotes;
                    }
                    else
                    {
                        existingTransaction.OldBalance = transaction.OldBalance;
                        existingTransaction.CashCollection = transaction.CashCollection;
                        existingTransaction.ElectronicCollection = transaction.ElectronicCollection;
                        existingTransaction.AdditionAmount = transaction.AdditionAmount;
                        existingTransaction.AdditionNotes = transaction.AdditionNotes;
                        existingTransaction.DiscountAmount = transaction.DiscountAmount;
                        existingTransaction.DiscountNotes = transaction.DiscountNotes;
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = formSection == "meal"
                        ? "تم تعديل بيانات الوجبة بنجاح"
                        : "تم تعديل بيانات التحصيل بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransactionExists(transaction.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction("Profile", "Clients", new { id = transaction.ClientId });
            }

            var client = await _context.Clients.FindAsync(transaction.ClientId);
            ViewBag.ClientName = client?.Name ?? "";
            ViewBag.ActiveStep = formSection == "meal" ? "meal" : "accounts";
            return View(transaction);
        }

        private void TryParseMealWeightExpressions(Transaction transaction)
        {
            if (string.IsNullOrWhiteSpace(transaction.GrossWeightExpression))
                transaction.GrossWeight = 0;
            else if (ArithmeticExpressionParser.TryParse(transaction.GrossWeightExpression, out var grossWeight))
                transaction.GrossWeight = grossWeight;
            else
                ModelState.AddModelError(nameof(transaction.GrossWeightExpression), "اكتب وزنًا أو عملية حسابية صحيحة، مثل 900+15.");

            if (string.IsNullOrWhiteSpace(transaction.EmptyWeightExpression))
                transaction.EmptyWeight = 0;
            else if (ArithmeticExpressionParser.TryParse(transaction.EmptyWeightExpression, out var emptyWeight))
                transaction.EmptyWeight = emptyWeight;
            else
                ModelState.AddModelError(nameof(transaction.EmptyWeightExpression), "اكتب وزنًا أو عملية حسابية صحيحة، مثل 8*12.");
        }

        // POST: Transactions/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                transaction.IsDeleted = true; // Soft delete
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الوجبة بنجاح";
                return RedirectToAction("Profile", "Clients", new { id = transaction.ClientId });
            }
            return NotFound();
        }

        // GET: Transactions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var transaction = await _context.Transactions
                .Include(t => t.Client)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
                return NotFound();

            return View(transaction);
        }

        // GET: Transactions/Invoice/5
        public async Task<IActionResult> Invoice(int? id)
        {
            if (id == null)
                return NotFound();

            var transaction = await _context.Transactions
                .Include(t => t.Client)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (transaction?.Client == null)
                return NotFound();

            var sameDayTransactions = await _context.Transactions
                .Where(candidate =>
                    candidate.ClientId == transaction.ClientId &&
                    candidate.Date.Date == transaction.Date.Date &&
                    !candidate.IsDeleted)
                .OrderBy(candidate => candidate.Id)
                .ToListAsync();
            var accountingTransaction = sameDayTransactions
                .FirstOrDefault(HasAccountingData) ?? transaction;
            var mealTransaction = sameDayTransactions
                .FirstOrDefault(HasMealData) ?? transaction;
            var invoice = new InvoiceViewModel
            {
                Transaction = transaction,
                AccountingTransaction = accountingTransaction,
                MealTransaction = mealTransaction
            };

            var whatsappNumber = NormalizeEgyptianPhoneNumber(transaction.Client.Phone);
            ViewBag.WhatsAppUrl = whatsappNumber == null
                ? null
                : $"https://wa.me/{whatsappNumber}?text={Uri.EscapeDataString(BuildInvoiceMessage(invoice, transaction.Client))}";

            return View(invoice);
        }

        private static bool HasAccountingData(Transaction transaction) =>
            transaction.CashCollection != 0 ||
            transaction.ElectronicCollection != 0 ||
            transaction.AdditionAmount != 0 ||
            transaction.DiscountAmount != 0 ||
            !string.IsNullOrWhiteSpace(transaction.AdditionNotes) ||
            !string.IsNullOrWhiteSpace(transaction.DiscountNotes);

        private static bool HasMealData(Transaction transaction) =>
            transaction.GrossWeight != 0 ||
            transaction.EmptyWeight != 0 ||
            transaction.ExtraField != 0 ||
            transaction.FarmPrice != 0 ||
            transaction.PricePerKilo != 0 ||
            !string.IsNullOrWhiteSpace(transaction.MealType) ||
            !string.IsNullOrWhiteSpace(transaction.GeneralNotes);

        private static string? NormalizeMealType(string? mealType) =>
            string.IsNullOrWhiteSpace(mealType) ? null : mealType.Trim();

        private static string? NormalizeEgyptianPhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            var digits = new string(phone.Where(character => character >= '0' && character <= '9').ToArray());
            if (digits.StartsWith("00", StringComparison.Ordinal))
                digits = digits[2..];

            if (digits.StartsWith("20", StringComparison.Ordinal))
                return digits;

            if (digits.StartsWith('0'))
                return "20" + digits[1..];

            return "20" + digits;
        }

        private static string BuildInvoiceMessage(InvoiceViewModel invoice, Client client)
        {
            var transaction = invoice.Transaction;
            var accounting = invoice.AccountingTransaction;
            var meal = invoice.MealTransaction;
            var culture = CultureInfo.GetCultureInfo("ar-EG");
            string Amount(decimal value) => value.ToString("#,##0.####", culture);

            var lines = new List<string>
            {
                "بيرفكت",
                $"العميل: {client.Name}",
                $"التاريخ: {transaction.Date:yyyy/MM/dd}",
                $"فلوس عليه قديم: {Amount(accounting.OldBalance)}",
                $"تحصيل نقدي: {Amount(accounting.CashCollection)}",
                $"تحصيل إلكتروني: {Amount(accounting.ElectronicCollection)}",
                $"الخصم: {Amount(accounting.DiscountAmount)}",
                $"الباقي بعد التحصيل: {Amount(accounting.RemainingAfterCollection)}",
                $"القائم: {Amount(meal.GrossWeight)}",
                $"الفارغ: {Amount(meal.EmptyWeight)}",
                $"الصافي: {Amount(meal.NetWeight)}",
                $"نوع الوجبة: {(string.IsNullOrWhiteSpace(meal.MealType) ? "غير محدد" : meal.MealType)}",
                $"سعر الكيلو: {Amount(meal.PricePerKilo)}",
                $"إجمالي الوجبة: {Amount(meal.MealTotal)}",
                $"الإضافة: {Amount(accounting.AdditionAmount)}",
                $"ملاحظات الإضافة: {(string.IsNullOrWhiteSpace(accounting.AdditionNotes) ? "لا توجد" : accounting.AdditionNotes)}"
            };

            if (!string.IsNullOrWhiteSpace(meal.GeneralNotes))
                lines.Add($"ملاحظات بعد الصافي: {meal.GeneralNotes}");

            lines.Add($"إجمالي ما عليه: {Amount(accounting.SubTotal + meal.MealTotal)}");

            return string.Join(Environment.NewLine, lines);
        }

        private bool TransactionExists(int id)
        {
            return _context.Transactions.Any(e => e.Id == id);
        }
    }
}
