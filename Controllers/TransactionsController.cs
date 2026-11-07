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
        private const string OldBalanceEditPassword = "123";
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
                Date = DateTime.Today,
                GrossWeightExpression = "0",
                EmptyWeightExpression = "0",
                CashTreasuryAccount = TreasuryAccount.AbuMazenCash,
                ElectronicTreasuryAccount = TreasuryAccount.AbuMazenVodafoneCash
            };

            // Carry forward the client's overall balance shown on the profile.
            var transactions = await _context.Transactions
                .Where(t => t.ClientId == clientId && !t.IsDeleted)
                .ToListAsync();
            transaction.OldBalance = ClientBalanceCalculator.Calculate(transactions);

            ViewBag.ClientName = client.Name;
            ViewBag.ActiveStep = section == "meal" ? "meal" : "accounts";
            return View(transaction);
        }

        // POST: Transactions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Transaction transaction, string? oldBalancePassword = null)
        {
            // Remove navigation property from validation
            ModelState.Remove("Client");
            transaction.Date = NormalizeEntryDate(transaction.Date);
            TryParseMealWeightExpressions(transaction);

            var existingTransactions = await _context.Transactions
                .Where(item => item.ClientId == transaction.ClientId && !item.IsDeleted)
                .ToListAsync();

            if (existingTransactions.Count > 0)
            {
                // الرصيد القديم يُحسب تلقائيًا من حركات العميل، ولا يُؤخذ من الإدخال.
                transaction.OldBalance = ClientBalanceCalculator.Calculate(existingTransactions);
            }
            else if (transaction.OldBalance != 0 && !IsOldBalancePasswordValid(oldBalancePassword))
            {
                ModelState.AddModelError(string.Empty, "لا يمكن تعديل الرصيد القديم بدون كلمة المرور الصحيحة.");
                transaction.OldBalance = 0;
                ModelState.Remove(nameof(Transaction.OldBalance));
            }

            if (ModelState.IsValid)
            {
                transaction.PricePerKilo = transaction.FarmPrice + transaction.ExtraField;
                transaction.MealType = NormalizeMealType(transaction.MealType);
                transaction.IsDeleted = false;
                _context.Add(transaction);
                await _context.SaveChangesAsync();
                await SyncTreasuryEntriesForTransactionAsync(transaction);
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
        public async Task<IActionResult> CreateCollectionOnly(Transaction transaction, string? oldBalancePassword = null)
        {
            ModelState.Remove("Client");
            transaction.Date = NormalizeEntryDate(transaction.Date);

            transaction.GrossWeight = 0;
            transaction.EmptyWeight = 0;
            transaction.FarmPrice = 0;
            transaction.ExtraField = 0;
            transaction.PricePerKilo = 0;

            var existingTransactions = await _context.Transactions
                .Where(item => item.ClientId == transaction.ClientId && !item.IsDeleted)
                .ToListAsync();

            if (existingTransactions.Count > 0)
            {
                // الرصيد القديم يُحسب تلقائيًا من حركات العميل، ولا يُؤخذ من الإدخال.
                transaction.OldBalance = ClientBalanceCalculator.Calculate(existingTransactions);
            }
            else if (transaction.OldBalance != 0 && !IsOldBalancePasswordValid(oldBalancePassword))
            {
                ModelState.AddModelError(string.Empty, "لا يمكن تعديل الرصيد القديم بدون كلمة المرور الصحيحة.");
                transaction.OldBalance = 0;
                ModelState.Remove(nameof(Transaction.OldBalance));
            }

            if (ModelState.IsValid)
            {
                transaction.IsDeleted = false;
                _context.Add(transaction);
                await _context.SaveChangesAsync();
                await SyncTreasuryEntriesForTransactionAsync(transaction);
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
            transaction.Date = NormalizeEntryDate(transaction.Date);
            TryParseMealWeightExpressions(transaction);
            var previousTransactions = await _context.Transactions
                .Where(t => t.ClientId == transaction.ClientId && !t.IsDeleted)
                .ToListAsync();
            transaction.OldBalance = ClientBalanceCalculator.Calculate(previousTransactions);
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
                await SyncTreasuryEntriesForTransactionAsync(transaction);
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
            LogBulkMealDiagnostics("start", model, savePassword);
            if (savePassword != BulkMealSavePassword)
            {
                ModelState.AddModelError(string.Empty, "كلمة المرور غير صحيحة. أدخل كلمة المرور لحفظ الوجبات.");
                return View(await BuildBulkMealViewModelAsync(model));
            }

            if (model.Date == default)
                ModelState.AddModelError(nameof(model.Date), "اختر تاريخ الوجبات.");

            if (model.FarmPrice < 0)
                ModelState.AddModelError(nameof(model.FarmPrice), "سعر المزرعة لا يمكن أن يكون سالبًا.");
            if (model.FarmWeight < 0)
                ModelState.AddModelError(nameof(model.FarmWeight), "وزن المزرعة لا يمكن أن يكون سالبًا.");
            if ((model.FarmWeight > 0 || model.FarmAdditionAmount > 0 || model.FarmDiscountAmount > 0) &&
                string.IsNullOrWhiteSpace(model.FarmName))
                ModelState.AddModelError(nameof(model.FarmName), "اسم المزرعة مطلوب عند إدخال وزن أو تعديل حسابها.");
            if (model.FarmAdditionAmount > 0 && string.IsNullOrWhiteSpace(model.FarmAdditionStatement))
                ModelState.AddModelError(nameof(model.FarmAdditionStatement), "اكتب بيان الإضافة.");
            if (model.FarmDiscountAmount > 0 && string.IsNullOrWhiteSpace(model.FarmDiscountStatement))
                ModelState.AddModelError(nameof(model.FarmDiscountStatement), "اكتب بيان الخصم.");

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
            var entries = new List<(
                Client Client,
                BulkMealRowViewModel Row,
                decimal GrossWeight,
                decimal EmptyWeight,
                decimal PanehGrossWeight,
                decimal PanehEmptyWeight)>();

            for (var index = 0; index < model.Rows.Count; index++)
            {
                var row = model.Rows[index];
                if (!clientById.TryGetValue(row.ClientId, out var client))
                {
                    ModelState.AddModelError(string.Empty, "قائمة العملاء تغيرت. حدّث الصفحة وحاول مرة أخرى.");
                    continue;
                }

                var splitIntoRegularAndPaneh = client.Name.Trim() == "صفيطه";

                var hasInput = !string.IsNullOrWhiteSpace(row.GrossWeightExpression) ||
                               !string.IsNullOrWhiteSpace(row.EmptyWeightExpression) ||
                               !string.IsNullOrWhiteSpace(row.Notes) ||
                               (splitIntoRegularAndPaneh &&
                                (!string.IsNullOrWhiteSpace(row.PanehGrossWeightExpression) ||
                                 !string.IsNullOrWhiteSpace(row.PanehEmptyWeightExpression)));
                if (!hasInput)
                    continue;

                var grossWeight = 0m;
                var grossIsValid = splitIntoRegularAndPaneh &&
                    string.IsNullOrWhiteSpace(row.GrossWeightExpression);
                if (!grossIsValid)
                    grossIsValid = ArithmeticExpressionParser.TryParse(row.GrossWeightExpression, out grossWeight);
                if (splitIntoRegularAndPaneh &&
                    string.IsNullOrWhiteSpace(row.GrossWeightExpression) &&
                    string.IsNullOrWhiteSpace(row.PanehGrossWeightExpression))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"أدخل القائم للعادي أو البانيه للعميلة {client.Name}.");
                }
                var emptyIsValid = string.IsNullOrWhiteSpace(row.EmptyWeightExpression) ||
                                   ArithmeticExpressionParser.TryParse(row.EmptyWeightExpression, out _);
                var emptyWeight = 0m;
                if (emptyIsValid && !string.IsNullOrWhiteSpace(row.EmptyWeightExpression))
                    ArithmeticExpressionParser.TryParse(row.EmptyWeightExpression, out emptyWeight);
                var panehGrossIsValid = !splitIntoRegularAndPaneh ||
                    string.IsNullOrWhiteSpace(row.PanehGrossWeightExpression) ||
                    ArithmeticExpressionParser.TryParse(row.PanehGrossWeightExpression, out _);
                var panehGrossWeight = 0m;
                if (splitIntoRegularAndPaneh &&
                    !string.IsNullOrWhiteSpace(row.PanehGrossWeightExpression))
                {
                    ArithmeticExpressionParser.TryParse(row.PanehGrossWeightExpression, out panehGrossWeight);
                }
                var panehEmptyIsValid = !splitIntoRegularAndPaneh ||
                    string.IsNullOrWhiteSpace(row.PanehEmptyWeightExpression) ||
                    ArithmeticExpressionParser.TryParse(row.PanehEmptyWeightExpression, out _);
                var panehEmptyWeight = 0m;
                if (splitIntoRegularAndPaneh &&
                    !string.IsNullOrWhiteSpace(row.PanehEmptyWeightExpression))
                {
                    ArithmeticExpressionParser.TryParse(row.PanehEmptyWeightExpression, out panehEmptyWeight);
                }

                if (!grossIsValid)
                    ModelState.AddModelError($"Rows[{index}].GrossWeightExpression", "اكتب رقمًا أو عملية جمع صحيحة.");
                if (!emptyIsValid)
                    ModelState.AddModelError($"Rows[{index}].EmptyWeightExpression", "اكتب رقمًا أو عملية ضرب صحيحة.");
                if (!panehGrossIsValid)
                    ModelState.AddModelError($"Rows[{index}].PanehGrossWeightExpression", "اكتب رقمًا أو عملية جمع صحيحة.");
                if (!panehEmptyIsValid)
                    ModelState.AddModelError($"Rows[{index}].PanehEmptyWeightExpression", "اكتب رقمًا أو عملية ضرب صحيحة.");
                if (grossIsValid && grossWeight < 0 || emptyIsValid && emptyWeight < 0 ||
                    panehGrossIsValid && panehGrossWeight < 0 ||
                    panehEmptyIsValid && panehEmptyWeight < 0)
                    ModelState.AddModelError(string.Empty, $"الأوزان لا يمكن أن تكون سالبة للعميل {client.Name}.");

                if (grossIsValid && emptyIsValid && panehGrossIsValid && panehEmptyIsValid)
                    entries.Add((client, row, grossWeight, emptyWeight, panehGrossWeight, panehEmptyWeight));
            }

            if (!ModelState.IsValid)
            {
                LogBulkMealDiagnostics("invalid", model, savePassword, entries.Count, ModelState);
                return View(await BuildBulkMealViewModelAsync(model));
            }

            if (entries.Any(entry => entry.Client.Name.Trim() == "صفيطه") &&
                model.MealType?.Length > 92)
            {
                ModelState.AddModelError(
                    nameof(model.MealType),
                    "اختصر نوع الوجبة إلى 92 حرفًا عند تقسيمها إلى عادي وبانيه.");
                return View(await BuildBulkMealViewModelAsync(model));
            }

            var netWeightTotal = entries.Sum(entry =>
                entry.GrossWeight - entry.EmptyWeight +
                (entry.Client.Name.Trim() == "صفيطه"
                    ? entry.PanehGrossWeight - entry.PanehEmptyWeight
                    : 0));
            var mealTotal = entries.Sum(entry =>
            {
                var regularNetWeight = entry.GrossWeight - entry.EmptyWeight;
                var regularTotal = regularNetWeight * (model.FarmPrice + entry.Row.PriceIncrement);
                if (entry.Client.Name.Trim() != "صفيطه")
                    return regularTotal;

                var panehNetWeight = entry.PanehGrossWeight - entry.PanehEmptyWeight;
                return regularTotal + panehNetWeight * (model.FarmPrice + entry.Row.PanehPriceIncrement);
            });
            FarmMealBatch? farmBatch = null;
            if (entries.Count > 0)
            {
                var farmName = model.FarmName?.Trim() ?? string.Empty;
                var previousFarmBalance = 0m;
                if (!string.IsNullOrEmpty(farmName))
                {
                    var previousMealBalance = await _context.FarmMealBatches
                        .AsNoTracking()
                        .Where(batch => batch.FarmName == farmName)
                        .Select(batch => batch.FarmPrice * batch.FarmWeight
                            + batch.FarmAdditionAmount
                            - batch.FarmDiscountAmount
                            - batch.FarmPaidAmount)
                        .SumAsync();
                    var paymentsBeforeMeal = await _context.FarmPayments
                        .AsNoTracking()
                        .Where(payment => payment.FarmName == farmName && payment.Date < model.Date.Date)
                        .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
                    var adjustmentsBeforeMeal = await _context.FarmAccountAdjustments
                        .AsNoTracking()
                        .Where(adjustment => adjustment.FarmName == farmName && adjustment.Date < model.Date.Date)
                        .Select(adjustment => adjustment.Type == FarmAccountAdjustmentType.Addition
                            ? adjustment.Amount
                            : -adjustment.Amount)
                        .SumAsync();
                    previousFarmBalance = previousMealBalance - paymentsBeforeMeal + adjustmentsBeforeMeal;
                }

                farmBatch = new FarmMealBatch
                {
                    Date = model.Date.Date,
                    FarmName = farmName,
                    FarmWeight = model.FarmWeight,
                    FarmPrice = model.FarmPrice,
                    FarmAdditionAmount = model.FarmAdditionAmount,
                    FarmAdditionStatement = string.IsNullOrWhiteSpace(model.FarmAdditionStatement)
                        ? null
                        : model.FarmAdditionStatement.Trim(),
                    FarmDiscountAmount = model.FarmDiscountAmount,
                    FarmDiscountStatement = string.IsNullOrWhiteSpace(model.FarmDiscountStatement)
                        ? null
                        : model.FarmDiscountStatement.Trim(),
                    PreviousFarmBalance = previousFarmBalance,
                    NetWeight = netWeightTotal,
                    MealTotal = mealTotal
                };
                _context.FarmMealBatches.Add(farmBatch);
            }

            foreach (var entry in entries)
            {
                var history = historyByClient.GetValueOrDefault(entry.Client.Id) ?? new List<Transaction>();
                var oldBalance = ClientBalanceCalculator.Calculate(history);
                var notes = string.IsNullOrWhiteSpace(entry.Row.Notes)
                    ? null
                    : entry.Row.Notes.Trim();
                if (entry.Client.Name.Trim() == "صفيطه")
                {
                    _context.Transactions.AddRange(
                        new Transaction
                        {
                            ClientId = entry.Client.Id,
                            FarmMealBatch = farmBatch,
                            Date = model.Date.Date,
                            OldBalance = oldBalance,
                            GrossWeight = entry.GrossWeight,
                            EmptyWeight = entry.EmptyWeight,
                            FarmPrice = model.FarmPrice,
                            ExtraField = entry.Row.PriceIncrement,
                            PricePerKilo = model.FarmPrice + entry.Row.PriceIncrement,
                            MealType = BuildSplitMealType(model.MealType, "عادي"),
                            GeneralNotes = notes,
                            IsDeleted = false
                        },
                        new Transaction
                        {
                            ClientId = entry.Client.Id,
                            FarmMealBatch = farmBatch,
                            Date = model.Date.Date,
                            GrossWeight = entry.PanehGrossWeight,
                            EmptyWeight = entry.PanehEmptyWeight,
                            FarmPrice = model.FarmPrice,
                            ExtraField = entry.Row.PanehPriceIncrement,
                            PricePerKilo = model.FarmPrice + entry.Row.PanehPriceIncrement,
                            MealType = BuildSplitMealType(model.MealType, "بانيه"),
                            GeneralNotes = notes,
                            IsDeleted = false
                        });
                }
                else
                {
                    _context.Transactions.Add(new Transaction
                    {
                        ClientId = entry.Client.Id,
                        FarmMealBatch = farmBatch,
                        Date = model.Date.Date,
                        OldBalance = oldBalance,
                        GrossWeight = entry.GrossWeight,
                        EmptyWeight = entry.EmptyWeight,
                        FarmPrice = model.FarmPrice,
                        ExtraField = entry.Row.PriceIncrement,
                        PricePerKilo = model.FarmPrice + entry.Row.PriceIncrement,
                        MealType = NormalizeMealType(model.MealType),
                        GeneralNotes = notes,
                        IsDeleted = false
                    });
                }
            }

            // احفظ الزيادات المكتوبة لكل عميل حتى تظل موجودة في المرات القادمة.
            foreach (var row in model.Rows)
            {
                if (!clientById.TryGetValue(row.ClientId, out var rowClient))
                    continue;

                rowClient.DefaultPriceIncrement = row.PriceIncrement;
                if (rowClient.Name.Trim() == "صفيطه")
                    rowClient.DefaultPanehPriceIncrement = row.PanehPriceIncrement;
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

            LogBulkMealDiagnostics("done", model, savePassword, entries.Count, ModelState);
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

        public async Task<IActionResult> FarmAccounts()
        {
            var batches = await _context.FarmMealBatches
                .AsNoTracking()
                .Select(batch => new
                {
                    batch.FarmName,
                    batch.FarmWeight,
                    batch.FarmPrice,
                    batch.NetWeight,
                    FarmAccount = batch.FarmPrice * batch.FarmWeight,
                    batch.FarmAdditionAmount,
                    batch.FarmDiscountAmount,
                    batch.FarmPaidAmount,
                    batch.MealTotal
                })
                .ToListAsync();

            var payments = await _context.FarmPayments
                .AsNoTracking()
                .Select(payment => new { payment.FarmName, payment.Amount })
                .ToListAsync();
            var adjustments = await _context.FarmAccountAdjustments
                .AsNoTracking()
                .Select(adjustment => new { adjustment.FarmName, adjustment.Type, adjustment.Amount })
                .ToListAsync();
            var paidByFarm = payments
                .GroupBy(payment => payment.FarmName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount), StringComparer.OrdinalIgnoreCase);

            var farms = batches
                .GroupBy(
                    batch => string.IsNullOrWhiteSpace(batch.FarmName)
                        ? "غير محدد"
                        : batch.FarmName.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var farmAccount = group.Sum(batch => batch.FarmAccount);
                    var addition = group.Sum(batch => batch.FarmAdditionAmount);
                    var discount = group.Sum(batch => batch.FarmDiscountAmount);
                    var farmAdjustments = adjustments
                        .Where(adjustment => string.Equals(adjustment.FarmName.Trim(), group.Key, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    addition += farmAdjustments
                        .Where(adjustment => adjustment.Type == FarmAccountAdjustmentType.Addition)
                        .Sum(adjustment => adjustment.Amount);
                    discount += farmAdjustments
                        .Where(adjustment => adjustment.Type == FarmAccountAdjustmentType.Discount)
                        .Sum(adjustment => adjustment.Amount);
                    var recordedPaid = paidByFarm.TryGetValue(group.Key, out var farmPayments) ? farmPayments : 0m;
                    var paid = group.Sum(batch => batch.FarmPaidAmount) + recordedPaid;
                    var balance = farmAccount + addition - discount - paid;
                    return new FarmAccountSummaryItemViewModel
                    {
                        FarmName = group.Key,
                        BatchCount = group.Count(),
                        FarmWeight = group.Sum(batch => batch.FarmWeight),
                        FarmAccount = farmAccount,
                        Addition = addition,
                        Discount = discount,
                        Paid = paid,
                        RemainingOwed = Math.Max(balance, 0),
                        Credit = Math.Max(-balance, 0),
                        MealSales = group.Sum(batch => batch.MealTotal),
                        Profit = group.Sum(batch => batch.MealTotal - batch.FarmPrice * batch.FarmWeight)
                    };
                })
                .OrderBy(farm => farm.FarmName)
                .ToList();

            return View(new FarmAccountSummaryViewModel
            {
                Farms = farms,
                TotalFarmAccount = farms.Sum(farm => farm.FarmAccount),
                TotalAddition = farms.Sum(farm => farm.Addition),
                TotalDiscount = farms.Sum(farm => farm.Discount),
                TotalPaid = farms.Sum(farm => farm.Paid),
                TotalRemainingOwed = farms.Sum(farm => farm.RemainingOwed),
                TotalCredit = farms.Sum(farm => farm.Credit),
                TotalMealSales = farms.Sum(farm => farm.MealSales),
                TotalProfit = farms.Sum(farm => farm.Profit)
            });
        }

        public async Task<IActionResult> FarmAccountDetails(string farmName)
        {
            if (string.IsNullOrWhiteSpace(farmName))
                return BadRequest();

            var normalizedFarmName = farmName.Trim();
            var batches = await _context.FarmMealBatches
                .AsNoTracking()
                .Where(batch => batch.FarmName.Trim() == normalizedFarmName)
                .Include(batch => batch.Transactions.Where(transaction => !transaction.IsDeleted))
                    .ThenInclude(transaction => transaction.Client)
                .OrderBy(batch => batch.Date)
                .ThenBy(batch => batch.Id)
                .ToListAsync();
            if (batches.Count == 0)
                return NotFound();

            var payments = await _context.FarmPayments
                .AsNoTracking()
                .Include(payment => payment.TreasuryEntry)
                .Where(payment => payment.FarmName.Trim() == normalizedFarmName)
                .OrderBy(payment => payment.Date)
                .ThenBy(payment => payment.Id)
                .ToListAsync();
            var adjustments = await _context.FarmAccountAdjustments
                .AsNoTracking()
                .Where(adjustment => adjustment.FarmName.Trim() == normalizedFarmName)
                .OrderBy(adjustment => adjustment.Date)
                .ThenBy(adjustment => adjustment.Id)
                .ToListAsync();

            var ledger = new FarmAccountLedgerViewModel
            {
                FarmName = normalizedFarmName,
                TotalFarmAccount = batches.Sum(batch => batch.FarmPrice * batch.FarmWeight),
                TotalAddition = batches.Sum(batch => batch.FarmAdditionAmount)
                    + adjustments.Where(adjustment => adjustment.Type == FarmAccountAdjustmentType.Addition).Sum(adjustment => adjustment.Amount),
                TotalDiscount = batches.Sum(batch => batch.FarmDiscountAmount)
                    + adjustments.Where(adjustment => adjustment.Type == FarmAccountAdjustmentType.Discount).Sum(adjustment => adjustment.Amount),
                LegacyPaid = batches.Sum(batch => batch.FarmPaidAmount),
                RecordedPaid = payments.Sum(payment => payment.Amount),
                TotalCustomerCollections = batches
                    .SelectMany(batch => batch.Transactions)
                    .Sum(transaction => transaction.CashCollection + transaction.ElectronicCollection),
                NewPayment = new FarmPaymentInputViewModel
                {
                    FarmName = normalizedFarmName,
                    Date = DateTime.Today
                },
                NewAdjustment = new FarmAccountAdjustmentInputViewModel
                {
                    FarmName = normalizedFarmName,
                    Date = DateTime.Today
                }
            };

            var movements = new List<(DateTime Date, int Priority, int Id, FarmAccountLedgerRowViewModel Row)>();
            foreach (var batch in batches)
            {
                var customerCollections = batch.Transactions
                    .Where(transaction => transaction.CashCollection > 0 || transaction.ElectronicCollection > 0)
                    .OrderBy(transaction => transaction.Date)
                    .ThenBy(transaction => transaction.Id)
                    .Select(transaction => new FarmCustomerCollectionViewModel
                    {
                        Date = transaction.Date,
                        ClientName = transaction.Client?.Name ?? "عميل محذوف",
                        Cash = transaction.CashCollection,
                        Electronic = transaction.ElectronicCollection
                    })
                    .ToList();

                movements.Add((batch.Date, 0, batch.Id, new FarmAccountLedgerRowViewModel
                {
                    Id = batch.Id,
                    Date = batch.Date,
                    Type = "وجبة",
                    Statement = $"وجبة للمزرعة - وزن {batch.FarmWeight:#,##0.####} × سعر {batch.FarmPrice:#,##0.####}" +
                        (batch.FarmAdditionAmount != 0
                            ? $"، إضافة {batch.FarmAdditionAmount:#,##0.####}" +
                                (string.IsNullOrWhiteSpace(batch.FarmAdditionStatement) ? string.Empty : $" ({batch.FarmAdditionStatement})")
                            : string.Empty) +
                        (batch.FarmDiscountAmount != 0
                            ? $"، خصم {batch.FarmDiscountAmount:#,##0.####}" +
                                (string.IsNullOrWhiteSpace(batch.FarmDiscountStatement) ? string.Empty : $" ({batch.FarmDiscountStatement})")
                            : string.Empty),
                    Debit = batch.FarmPrice * batch.FarmWeight + batch.FarmAdditionAmount,
                    Credit = batch.FarmDiscountAmount,
                    CustomerMeals = batch.Transactions
                        .OrderBy(transaction => transaction.Date)
                        .ThenBy(transaction => transaction.Id)
                        .Select(transaction => new FarmCustomerMealViewModel
                        {
                            Date = transaction.Date,
                            ClientName = transaction.Client?.Name ?? "عميل محذوف",
                            NetWeight = transaction.NetWeight,
                            MealTotal = transaction.MealTotal
                        })
                        .ToList(),
                    CustomerCollections = customerCollections
                }));
            }

            movements.AddRange(payments.Select(payment =>
                (payment.Date, 2, payment.Id, new FarmAccountLedgerRowViewModel
                {
                    Id = payment.Id,
                    Date = payment.Date,
                    Type = "دفعة للمزرعة",
                    Statement = payment.TreasuryEntry == null
                        ? payment.Statement
                        : $"{payment.Statement} - من {TreasuryEntry.GetAccountName(payment.TreasuryEntry.Account)}",
                    Credit = payment.Amount
                })));

            movements.AddRange(adjustments.Select(adjustment =>
            {
                var isAddition = adjustment.Type == FarmAccountAdjustmentType.Addition;
                return (adjustment.Date, 1, adjustment.Id, new FarmAccountLedgerRowViewModel
                {
                    Id = adjustment.Id,
                    Date = adjustment.Date,
                    Type = isAddition ? "إضافة على حساب المزرعة" : "خصم من حساب المزرعة",
                    Statement = adjustment.Statement,
                    Debit = isAddition ? adjustment.Amount : 0,
                    Credit = isAddition ? 0 : adjustment.Amount
                });
            }));

            var runningBalance = -ledger.LegacyPaid;
            if (ledger.LegacyPaid != 0)
            {
                ledger.Rows.Add(new FarmAccountLedgerRowViewModel
                {
                    Type = "مدفوع سابق غير مؤرخ",
                    Statement = "إجمالي الدفعات القديمة المسجلة دون تاريخ مستقل",
                    Credit = ledger.LegacyPaid,
                    Balance = runningBalance
                });
            }

            foreach (var movement in movements
                .OrderBy(item => item.Date)
                .ThenBy(item => item.Priority)
                .ThenBy(item => item.Id))
            {
                runningBalance += movement.Row.Debit - movement.Row.Credit;
                movement.Row.Balance = runningBalance;
                ledger.Rows.Add(movement.Row);
            }

            ledger.Rows.Reverse();
            return View(ledger);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFarmAccountAdjustment(
            [Bind(Prefix = "NewAdjustment")] FarmAccountAdjustmentInputViewModel model)
        {
            model.FarmName = model.FarmName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model.FarmName) ||
                !await _context.FarmMealBatches.AnyAsync(batch => batch.FarmName.Trim() == model.FarmName))
                ModelState.AddModelError(nameof(model.FarmName), "اختر مزرعة مسجلة.");
            if (!Enum.IsDefined(model.Type))
                ModelState.AddModelError(nameof(model.Type), "اختر إضافة أو خصم.");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "تأكد من إدخال بيانات الحركة بشكل صحيح.";
                return RedirectToAction(nameof(FarmAccountDetails), new { farmName = model.FarmName });
            }

            _context.FarmAccountAdjustments.Add(new FarmAccountAdjustment
            {
                FarmName = model.FarmName,
                Type = model.Type,
                Amount = model.Amount,
                Date = model.Date.Date,
                Statement = model.Statement.Trim()
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = model.Type == FarmAccountAdjustmentType.Addition
                ? "تم تسجيل الإضافة على حساب المزرعة."
                : "تم تسجيل الخصم من حساب المزرعة.";
            return RedirectToAction(nameof(FarmAccountDetails), new { farmName = model.FarmName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFarmPayment(
            [Bind(Prefix = "NewPayment")] FarmPaymentInputViewModel model)
        {
            model.FarmName = model.FarmName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model.FarmName) ||
                !await _context.FarmMealBatches.AnyAsync(batch => batch.FarmName.Trim() == model.FarmName))
                ModelState.AddModelError(nameof(model.FarmName), "اختر مزرعة مسجلة.");
            if (!FarmPaymentInputViewModel.AvailableTreasuryAccounts.Contains(model.TreasuryAccount))
                ModelState.AddModelError(nameof(model.TreasuryAccount), "اختر خزنة صحيحة.");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "تأكد من إدخال بيانات الدفعة بشكل صحيح.";
                return RedirectToAction(nameof(FarmAccountDetails), new { farmName = model.FarmName });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var availableBalance = await _context.TreasuryEntries
                .Where(entry => entry.Account == model.TreasuryAccount)
                .SumAsync(entry => entry.Incoming - entry.Outgoing);
            if (model.Amount > availableBalance)
            {
                await transaction.RollbackAsync();
                TempData["Error"] =
                    $"الرصيد غير كافٍ في {TreasuryEntry.GetAccountName(model.TreasuryAccount)}. الرصيد الحالي: {availableBalance:#,##0.####}.";
                return RedirectToAction(nameof(FarmAccountDetails), new { farmName = model.FarmName });
            }

            var paymentStatement = model.Statement.Trim();
            var treasuryStatement = $"دفعة للمزرعة: {model.FarmName} - {paymentStatement}";
            _context.FarmPayments.Add(new FarmPayment
            {
                FarmName = model.FarmName,
                Amount = model.Amount,
                Date = model.Date.Date,
                Statement = paymentStatement,
                TreasuryEntry = new TreasuryEntry
                {
                    Account = model.TreasuryAccount,
                    Statement = treasuryStatement[..Math.Min(treasuryStatement.Length, 500)],
                    Outgoing = model.Amount,
                    Date = model.Date.Date
                }
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] =
                $"تم تسجيل دفعة المزرعة وخصمها من {TreasuryEntry.GetAccountName(model.TreasuryAccount)}.";
            return RedirectToAction(nameof(FarmAccountDetails), new { farmName = model.FarmName });
        }

        [HttpGet]
        public async Task<IActionResult> EditFarmPayment(int id)
        {
            var payment = await _context.FarmPayments
                .Include(item => item.TreasuryEntry)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
            if (payment == null)
                return NotFound();

            return View(new FarmPaymentEditViewModel
            {
                Id = payment.Id,
                FarmName = payment.FarmName,
                Amount = payment.Amount,
                Date = payment.Date,
                Statement = payment.Statement,
                TreasuryAccount = payment.TreasuryEntry?.Account ?? TreasuryAccount.General,
                HasTreasuryEntry = payment.TreasuryEntry != null
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFarmPayment(FarmPaymentEditViewModel model)
        {
            var payment = await _context.FarmPayments
                .Include(item => item.TreasuryEntry)
                .FirstOrDefaultAsync(item => item.Id == model.Id);
            if (payment == null)
                return NotFound();

            if (payment.TreasuryEntry != null &&
                !FarmPaymentInputViewModel.AvailableTreasuryAccounts.Contains(model.TreasuryAccount))
                ModelState.AddModelError(nameof(model.TreasuryAccount), "اختر خزنة صحيحة.");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "تأكد من إدخال بيانات الدفعة بشكل صحيح.";
                return RedirectToAction(nameof(EditFarmPayment), new { id = payment.Id });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (payment.TreasuryEntry != null)
            {
                var availableBalance = await _context.TreasuryEntries
                    .Where(entry => entry.Account == model.TreasuryAccount)
                    .SumAsync(entry => entry.Incoming - entry.Outgoing);
                if (payment.TreasuryEntry.Account == model.TreasuryAccount)
                    availableBalance += payment.TreasuryEntry.Outgoing;

                if (model.Amount > availableBalance)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] =
                        $"الرصيد غير كافٍ في {TreasuryEntry.GetAccountName(model.TreasuryAccount)}. المتاح بعد استبعاد الدفعة الحالية: {availableBalance:#,##0.####}.";
                    return RedirectToAction(nameof(EditFarmPayment), new { id = payment.Id });
                }
            }

            payment.Amount = model.Amount;
            payment.Date = model.Date.Date;
            payment.Statement = model.Statement.Trim();
            if (payment.TreasuryEntry != null)
            {
                var treasuryStatement = $"دفعة للمزرعة: {payment.FarmName} - {payment.Statement}";
                payment.TreasuryEntry.Account = model.TreasuryAccount;
                payment.TreasuryEntry.Statement = treasuryStatement[..Math.Min(treasuryStatement.Length, 500)];
                payment.TreasuryEntry.Outgoing = model.Amount;
                payment.TreasuryEntry.Incoming = 0;
                payment.TreasuryEntry.Date = model.Date.Date;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = payment.TreasuryEntry != null
                ? "تم تعديل الدفعة وتحديث حركة الخزنة المرتبطة بها."
                : "تم تعديل الدفعة القديمة، وهي غير مرتبطة بحركة خزنة.";
            return RedirectToAction(nameof(FarmAccountDetails), new { farmName = payment.FarmName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFarmPayment(int id)
        {
            var payment = await _context.FarmPayments
                .Include(item => item.TreasuryEntry)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (payment == null)
                return NotFound();

            var farmName = payment.FarmName;
            var treasuryEntry = payment.TreasuryEntry;
            var hasTreasuryEntry = treasuryEntry != null;
            if (treasuryEntry != null)
                _context.TreasuryEntries.Remove(treasuryEntry);
            _context.FarmPayments.Remove(payment);
            await _context.SaveChangesAsync();
            TempData["Success"] = hasTreasuryEntry
                ? "تم حذف دفعة المزرعة وإلغاء خصمها من الخزنة."
                : "تم حذف دفعة المزرعة.";
            return RedirectToAction(nameof(FarmAccountDetails), new { farmName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFarmAccountAdjustment(int id)
        {
            var adjustment = await _context.FarmAccountAdjustments
                .FirstOrDefaultAsync(item => item.Id == id);
            if (adjustment == null)
                return NotFound();

            var farmName = adjustment.FarmName;
            _context.FarmAccountAdjustments.Remove(adjustment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حذف حركة تعديل حساب المزرعة.";
            return RedirectToAction(nameof(FarmAccountDetails), new { farmName });
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

            foreach (var transaction in batch.Transactions)
            {
                transaction.Date = batch.Date;
                transaction.FarmPrice = batch.FarmPrice;
                transaction.PricePerKilo = batch.FarmPrice + transaction.ExtraField;
            }

            await RefreshFarmMealBatchAsync(batch);

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تعديل بيانات الوجبة بنجاح.";
            return RedirectToAction(nameof(FarmMeals), new
            {
                page = model.ReturnPage,
                pageSize = model.ReturnPageSize,
                farmName = model.ReturnFarmName
            });
        }

        private async Task RefreshFarmMealBatchAsync(FarmMealBatch batch)
        {
            var activeTransactions = batch.Transactions
                .Where(transaction => !transaction.IsDeleted)
                .ToList();
            batch.NetWeight = activeTransactions.Sum(transaction => transaction.NetWeight);
            batch.MealTotal = activeTransactions.Sum(transaction => transaction.MealTotal);

            var expense = await _context.ExpenseEntries
                .FirstOrDefaultAsync(entry => entry.FarmMealBatchId == batch.Id);
            var profit = batch.Profit;
            if (profit == 0)
            {
                if (expense != null)
                    _context.ExpenseEntries.Remove(expense);
                return;
            }

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
                .Select(batch => batch.FarmPrice * batch.FarmWeight + batch.FarmAdditionAmount - batch.FarmDiscountAmount - batch.FarmPaidAmount)
                .SumAsync()
                - (await _context.FarmPayments
                    .AsNoTracking()
                    .Where(payment => payment.FarmName == normalizedFarmName)
                    .SumAsync(payment => (decimal?)payment.Amount) ?? 0m)
                + (await _context.FarmAccountAdjustments
                    .AsNoTracking()
                    .Where(adjustment => adjustment.FarmName == normalizedFarmName)
                    .SumAsync(adjustment => (decimal?)(adjustment.Type == FarmAccountAdjustmentType.Addition
                        ? adjustment.Amount
                        : -adjustment.Amount)) ?? 0m);

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
                    previousBatch.FarmPrice * previousBatch.FarmWeight + previousBatch.FarmAdditionAmount - previousBatch.FarmDiscountAmount - previousBatch.FarmPaidAmount)
                .SumAsync()
                - (await _context.FarmPayments
                    .AsNoTracking()
                    .Where(payment => payment.FarmName == batch.FarmName && payment.Date < batch.Date)
                    .SumAsync(payment => (decimal?)payment.Amount) ?? 0m)
                + (await _context.FarmAccountAdjustments
                    .AsNoTracking()
                    .Where(adjustment => adjustment.FarmName == batch.FarmName && adjustment.Date < batch.Date)
                    .SumAsync(adjustment => (decimal?)(adjustment.Type == FarmAccountAdjustmentType.Addition
                        ? adjustment.Amount
                        : -adjustment.Amount)) ?? 0m);

            batch.Transactions = batch.Transactions
                .OrderBy(transaction => transaction.Client?.Name)
                .ThenBy(transaction => transaction.Id)
                .ToList();

            var includedClientIds = batch.Transactions
                .Where(transaction => !transaction.IsDeleted)
                .Select(transaction => transaction.ClientId)
                .Distinct()
                .ToList();
            ViewBag.AvailableClients = await _context.Clients
                .AsNoTracking()
                .Where(client => client.IsActive && !includedClientIds.Contains(client.Id))
                .OrderBy(client => client.Name)
                .ToListAsync();
            var defaultMealType = batch.Transactions
                .Where(transaction => !transaction.IsDeleted && !string.IsNullOrWhiteSpace(transaction.MealType))
                .Select(transaction => transaction.MealType)
                .FirstOrDefault();
            ViewBag.DefaultMealType = defaultMealType?.EndsWith(" - عادي", StringComparison.Ordinal) == true
                ? defaultMealType[..^" - عادي".Length]
                : defaultMealType?.EndsWith(" - بانيه", StringComparison.Ordinal) == true
                    ? defaultMealType[..^" - بانيه".Length]
                    : defaultMealType;

            return View(batch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddClientToFarmMeal(int id, AddClientToFarmMealViewModel model)
        {
            var batch = await _context.FarmMealBatches
                .Include(item => item.Transactions)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (batch == null)
                return NotFound();

            var client = await _context.Clients
                .FirstOrDefaultAsync(item => item.Id == model.ClientId && item.IsActive);
            if (client == null)
                ModelState.AddModelError(nameof(model.ClientId), "اختر عميلًا نشطًا.");
            else if (batch.Transactions.Any(transaction =>
                         transaction.ClientId == client.Id && !transaction.IsDeleted))
                ModelState.AddModelError(nameof(model.ClientId), "هذا العميل موجود بالفعل في الوجبة.");

            var isPanehClient = client?.Name.Trim() == "صفيطه";
            var grossWeight = 0m;
            var emptyWeight = 0m;
            var panehGrossWeight = 0m;
            var panehEmptyWeight = 0m;
            var grossIsValid = string.IsNullOrWhiteSpace(model.GrossWeight)
                ? isPanehClient
                : ArithmeticExpressionParser.TryParse(model.GrossWeight, out grossWeight);
            var emptyIsValid = string.IsNullOrWhiteSpace(model.EmptyWeight) ||
                ArithmeticExpressionParser.TryParse(model.EmptyWeight, out emptyWeight);
            var panehGrossIsValid = string.IsNullOrWhiteSpace(model.PanehGrossWeight) ||
                ArithmeticExpressionParser.TryParse(model.PanehGrossWeight, out panehGrossWeight);
            var panehEmptyIsValid = string.IsNullOrWhiteSpace(model.PanehEmptyWeight) ||
                ArithmeticExpressionParser.TryParse(model.PanehEmptyWeight, out panehEmptyWeight);

            if (!grossIsValid)
                ModelState.AddModelError(nameof(model.GrossWeight), "اكتب رقمًا أو عملية جمع صحيحة للقائم.");
            if (!emptyIsValid)
                ModelState.AddModelError(nameof(model.EmptyWeight), "اكتب رقمًا أو عملية ضرب صحيحة للفارغ.");
            if (!panehGrossIsValid)
                ModelState.AddModelError(nameof(model.PanehGrossWeight), "اكتب رقمًا أو عملية جمع صحيحة لقائم البانيه.");
            if (!panehEmptyIsValid)
                ModelState.AddModelError(nameof(model.PanehEmptyWeight), "اكتب رقمًا أو عملية ضرب صحيحة لفارغ البانيه.");

            if (grossWeight < 0 || emptyWeight < 0 || panehGrossWeight < 0 || panehEmptyWeight < 0)
                ModelState.AddModelError(string.Empty, "الأوزان لا يمكن أن تكون سالبة.");

            if (grossIsValid && emptyIsValid && grossWeight < emptyWeight)
                ModelState.AddModelError(nameof(model.EmptyWeight), "الفارغ لا يمكن أن يكون أكبر من القائم.");
            if (panehGrossIsValid && panehEmptyIsValid && panehGrossWeight < panehEmptyWeight)
                ModelState.AddModelError(nameof(model.PanehEmptyWeight), "فارغ البانيه لا يمكن أن يكون أكبر من قائم البانيه.");

            if (isPanehClient)
            {
                var hasRegular = grossWeight > 0 || emptyWeight > 0;
                var hasPaneh = panehGrossWeight > 0 || panehEmptyWeight > 0;
                if (!hasRegular && !hasPaneh)
                    ModelState.AddModelError(string.Empty, "أدخل وزنًا للعادي أو البانيه.");
            }
            else
            {
                if (grossIsValid && grossWeight <= 0)
                    ModelState.AddModelError(nameof(model.GrossWeight), "أدخل وزن القائم أكبر من صفر.");
                if (panehGrossWeight != 0 || panehEmptyWeight != 0)
                    ModelState.AddModelError(string.Empty, "حقول البانيه متاحة للعميلة صفيطه فقط.");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "تأكد من إدخال بيانات العميل والوزن بشكل صحيح.";
                return RedirectToAction(nameof(FarmMealDetails), new { id });
            }

            var history = await _context.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.ClientId == client!.Id && !transaction.IsDeleted)
                .OrderBy(transaction => transaction.Id)
                .ToListAsync();
            var oldBalance = ClientBalanceCalculator.Calculate(history);
            var transactions = new List<Transaction>();
            var notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            if (isPanehClient)
            {
                if (grossWeight > 0 || emptyWeight > 0)
                {
                    transactions.Add(new Transaction
                    {
                        ClientId = client!.Id,
                        FarmMealBatchId = batch.Id,
                        Date = batch.Date,
                        OldBalance = oldBalance,
                        GrossWeight = grossWeight,
                        EmptyWeight = emptyWeight,
                        FarmPrice = batch.FarmPrice,
                        ExtraField = client.DefaultPriceIncrement,
                        PricePerKilo = batch.FarmPrice + client.DefaultPriceIncrement,
                        MealType = BuildSplitMealType(model.MealType, "عادي"),
                        GeneralNotes = notes
                    });
                }
                if (panehGrossWeight > 0 || panehEmptyWeight > 0)
                {
                    transactions.Add(new Transaction
                    {
                        ClientId = client!.Id,
                        FarmMealBatchId = batch.Id,
                        Date = batch.Date,
                        OldBalance = oldBalance,
                        GrossWeight = panehGrossWeight,
                        EmptyWeight = panehEmptyWeight,
                        FarmPrice = batch.FarmPrice,
                        ExtraField = client.DefaultPanehPriceIncrement,
                        PricePerKilo = batch.FarmPrice + client.DefaultPanehPriceIncrement,
                        MealType = BuildSplitMealType(model.MealType, "بانيه"),
                        GeneralNotes = notes
                    });
                }
            }
            else
            {
                transactions.Add(new Transaction
                {
                    ClientId = client!.Id,
                    FarmMealBatchId = batch.Id,
                    Date = batch.Date,
                    OldBalance = oldBalance,
                    GrossWeight = grossWeight,
                    EmptyWeight = emptyWeight,
                    FarmPrice = batch.FarmPrice,
                    ExtraField = client.DefaultPriceIncrement,
                    PricePerKilo = batch.FarmPrice + client.DefaultPriceIncrement,
                    MealType = NormalizeMealType(model.MealType),
                    GeneralNotes = notes
                });
            }

            _context.Transactions.AddRange(transactions);
            foreach (var transaction in transactions)
                batch.Transactions.Add(transaction);

            await RefreshFarmMealBatchAsync(batch);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم إضافة العميل {client!.Name} إلى الوجبة وتحديث إجمالي التوزيع.";
            return RedirectToAction(nameof(FarmMealDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFarmMealPricing(int id, decimal farmPrice, decimal farmWeight, decimal? amountPaid = null)
        {
            if (farmPrice < 0 || farmWeight < 0 || (amountPaid.HasValue && amountPaid.Value < 0))
            {
                TempData["Error"] = "يرجى إدخال قيم صحيحة وغير سالبة لسعر الكيلو ووزن المزرعة والمبلغ المدفوع.";
                return RedirectToAction(nameof(FarmMealDetails), new { id });
            }

            var batch = await _context.FarmMealBatches
                .Include(item => item.Transactions)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (batch == null)
                return NotFound();

            batch.FarmPrice = farmPrice;
            batch.FarmWeight = farmWeight;
            if (amountPaid.HasValue)
            {
                batch.FarmPaidAmount = amountPaid.Value;
            }

            foreach (var transaction in batch.Transactions)
            {
                transaction.FarmPrice = batch.FarmPrice;
                transaction.PricePerKilo = batch.FarmPrice + transaction.ExtraField;
            }

            await RefreshFarmMealBatchAsync(batch);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تحديث سعر الكيلو ووزن المزرعة وإعادة احتساب الحسابات بنجاح.";
            return RedirectToAction(nameof(FarmMealDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFarmPayment(
            int id,
            decimal amountPaid,
            decimal? farmPrice = null,
            decimal? farmWeight = null,
            decimal? farmAddition = null,
            decimal? farmDiscount = null)
        {
            if (amountPaid < 0 ||
                (farmPrice.HasValue && farmPrice.Value < 0) ||
                (farmWeight.HasValue && farmWeight.Value < 0) ||
                (farmAddition.HasValue && farmAddition.Value < 0) ||
                (farmDiscount.HasValue && farmDiscount.Value < 0))
            {
                TempData["Error"] = "أدخل مبالغ وقيمًا صحيحة غير سالبة.";
                return RedirectToAction(nameof(FarmMealDetails), new { id });
            }

            var batch = await _context.FarmMealBatches
                .Include(item => item.Transactions)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (batch == null)
                return NotFound();

            batch.FarmPaidAmount = amountPaid;
            if (farmAddition.HasValue)
                batch.FarmAdditionAmount = farmAddition.Value;
            if (farmDiscount.HasValue)
                batch.FarmDiscountAmount = farmDiscount.Value;
            if (farmPrice.HasValue)
                batch.FarmPrice = farmPrice.Value;
            if (farmWeight.HasValue)
                batch.FarmWeight = farmWeight.Value;

            if (farmPrice.HasValue || farmWeight.HasValue)
            {
                foreach (var transaction in batch.Transactions)
                {
                    transaction.FarmPrice = batch.FarmPrice;
                    transaction.PricePerKilo = batch.FarmPrice + transaction.ExtraField;
                }
            }

            await RefreshFarmMealBatchAsync(batch);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حفظ التعديلات بنجاح.";
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
                FarmPrice = submitted?.FarmPrice ?? 0,
                MealType = submitted?.MealType,
                FarmName = submitted?.FarmName ?? string.Empty,
                FarmWeight = submitted?.FarmWeight ?? 0,
                FarmAdditionAmount = submitted?.FarmAdditionAmount ?? 0,
                FarmAdditionStatement = submitted?.FarmAdditionStatement,
                FarmDiscountAmount = submitted?.FarmDiscountAmount ?? 0,
                FarmDiscountStatement = submitted?.FarmDiscountStatement,
                Rows = clients.Select(client =>
                {
                    var history = historyByClient.GetValueOrDefault(client.Id) ?? new List<Transaction>();
                    submittedByClient.TryGetValue(client.Id, out var submittedRow);
                    return new BulkMealRowViewModel
                    {
                        ClientId = client.Id,
                        ClientName = client.Name,
                        OldBalance = ClientBalanceCalculator.Calculate(history),
                        GrossWeightExpression = submittedRow?.GrossWeightExpression ?? string.Empty,
                        EmptyWeightExpression = submittedRow?.EmptyWeightExpression ?? string.Empty,
                        PanehGrossWeightExpression = submittedRow?.PanehGrossWeightExpression ?? string.Empty,
                        PanehEmptyWeightExpression = submittedRow?.PanehEmptyWeightExpression ?? string.Empty,
                        Notes = submittedRow?.Notes,
                        PriceIncrement = submittedRow?.PriceIncrement ?? client.DefaultPriceIncrement,
                        PanehPriceIncrement = submittedRow?.PanehPriceIncrement ?? client.DefaultPanehPriceIncrement
                    };
                }).ToList()
            };
        }

        // تشخيص مؤقت: يسجل بيانات الوجبات المستلمة لمعرفة سبب رسالة "أدخل بيانات وجبة".
        private static void LogBulkMealDiagnostics(
            string stage,
            BulkMealViewModel model,
            string? savePassword,
            int? entriesCount = null,
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary? modelState = null)
        {
            try
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bulkmeal-debug.log");
                var builder = new System.Text.StringBuilder();
                builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STAGE={stage} Rows={model.Rows.Count} PasswordOk={savePassword == BulkMealSavePassword} Farm='{model.FarmName}' Date='{model.Date:yyyy-MM-dd}' Price={model.FarmPrice} Entries={(entriesCount?.ToString() ?? "-")} Valid={(modelState?.IsValid.ToString() ?? "-")}");
                if (modelState != null)
                {
                    foreach (var error in modelState.Values.SelectMany(value => value.Errors))
                        builder.AppendLine($"  ModelError: {error.ErrorMessage}");
                }

                foreach (var row in model.Rows)
                {
                    if (string.IsNullOrWhiteSpace(row.GrossWeightExpression) &&
                        string.IsNullOrWhiteSpace(row.EmptyWeightExpression) &&
                        string.IsNullOrWhiteSpace(row.PanehGrossWeightExpression) &&
                        string.IsNullOrWhiteSpace(row.PanehEmptyWeightExpression) &&
                        string.IsNullOrWhiteSpace(row.Notes))
                        continue;

                    builder.AppendLine(
                        $"  ClientId={row.ClientId} Gross='{row.GrossWeightExpression}' Empty='{row.EmptyWeightExpression}' " +
                        $"PanehGross='{row.PanehGrossWeightExpression}' PanehEmpty='{row.PanehEmptyWeightExpression}' " +
                        $"Notes='{row.Notes}' Inc={row.PriceIncrement}/{row.PanehPriceIncrement}");
                }

                System.IO.File.AppendAllText(path, builder.ToString(), System.Text.Encoding.UTF8);
            }
            catch
            {
                // التشخيص لا يجب أن يعطل الحفظ.
            }
        }

        private static string? BuildSplitMealType(string? mealType, string variety)
        {
            var normalizedMealType = NormalizeMealType(mealType);
            return string.IsNullOrEmpty(normalizedMealType)
                ? variety
                : $"{normalizedMealType} - {variety}";
        }

        // GET: Transactions/Edit/5
        public async Task<IActionResult> Edit(
            int? id,
            string section = "accounts",
            int returnPage = 1,
            int returnPageSize = 10,
            DateTime? returnFromDate = null,
            DateTime? returnToDate = null)
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
            transaction.CashTreasuryAccount ??= TreasuryAccount.HajjImamCash;
            transaction.ElectronicTreasuryAccount ??= TreasuryAccount.HajjImamVodafoneCash;
            ViewBag.ClientName = transaction.Client?.Name ?? "";
            ViewBag.ActiveStep = section == "meal" ? "meal" : "accounts";
            ViewBag.FarmMealBatchId = section == "meal" ? transaction.FarmMealBatchId : null;
            ViewBag.ReturnPage = returnPage;
            ViewBag.ReturnPageSize = returnPageSize;
            ViewBag.ReturnFromDate = returnFromDate;
            ViewBag.ReturnToDate = returnToDate;
            return View(transaction);
        }

        // POST: Transactions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Transaction transaction,
            string formSection = "accounts",
            int returnPage = 1,
            int returnPageSize = 10,
            DateTime? returnFromDate = null,
            DateTime? returnToDate = null,
            string? oldBalancePassword = null)
        {
            if (id != transaction.Id)
                return NotFound();

            // Remove navigation property from validation
            ModelState.Remove("Client");
            if (formSection == "meal")
                TryParseMealWeightExpressions(transaction);

            var existingTransaction = await _context.Transactions.FindAsync(id);
            if (existingTransaction == null)
                return NotFound();

            // تعديل "فلوس عليه قديم" محمي بكلمة مرور.
            if (formSection != "meal" &&
                transaction.OldBalance != existingTransaction.OldBalance &&
                !IsOldBalancePasswordValid(oldBalancePassword))
            {
                ModelState.AddModelError(string.Empty, "لا يمكن تعديل الرصيد القديم بدون كلمة المرور الصحيحة.");
                transaction.OldBalance = existingTransaction.OldBalance;
                ModelState.Remove(nameof(Transaction.OldBalance));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var farmMealBatchId = formSection == "meal"
                        ? existingTransaction.FarmMealBatchId
                        : null;
                    FarmMealBatch? farmMealBatch = null;
                    if (farmMealBatchId.HasValue)
                    {
                        farmMealBatch = await _context.FarmMealBatches
                            .Include(item => item.Transactions)
                            .FirstOrDefaultAsync(item => item.Id == farmMealBatchId.Value);
                        if (farmMealBatch == null)
                            return NotFound();
                    }

                    var dateWasPosted = Request.Form.ContainsKey(nameof(Transaction.Date));
                    var requestedDate = dateWasPosted
                        ? NormalizeEntryDate(transaction.Date, existingTransaction.Date)
                        : existingTransaction.Date;

                    if (farmMealBatch != null)
                    {
                        // تاريخ الوجبة في ملف العميل يتبع دائمًا تاريخ الوجبات المسجل في إدخال الوجبات.
                        if (!dateWasPosted)
                            requestedDate = farmMealBatch.Date;

                        if (requestedDate != farmMealBatch.Date)
                        {
                            farmMealBatch.Date = requestedDate;
                            foreach (var batchTransaction in farmMealBatch.Transactions)
                                batchTransaction.Date = requestedDate;
                        }

                        existingTransaction.Date = farmMealBatch.Date;
                    }
                    else
                    {
                        existingTransaction.Date = requestedDate;
                    }

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
                        existingTransaction.CashTreasuryAccount = transaction.CashTreasuryAccount;
                        existingTransaction.ElectronicCollection = transaction.ElectronicCollection;
                        existingTransaction.ElectronicTreasuryAccount = transaction.ElectronicTreasuryAccount;
                        existingTransaction.AdditionAmount = transaction.AdditionAmount;
                        existingTransaction.AdditionNotes = transaction.AdditionNotes;
                        existingTransaction.DiscountAmount = transaction.DiscountAmount;
                        existingTransaction.DiscountNotes = transaction.DiscountNotes;
                    }

                    if (farmMealBatch != null)
                        await RefreshFarmMealBatchAsync(farmMealBatch);

                    await _context.SaveChangesAsync();
                    await SyncTreasuryEntriesForTransactionAsync(existingTransaction);
                    TempData["Success"] = formSection == "meal"
                        ? "تم تعديل بيانات الوجبة بنجاح"
                        : "تم تعديل بيانات التحصيل بنجاح";
                    if (farmMealBatchId.HasValue)
                        return RedirectToAction(nameof(FarmMealDetails), new { id = farmMealBatchId.Value });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransactionExists(transaction.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction("Profile", "Clients", new
                {
                    id = transaction.ClientId,
                    page = returnPage,
                    pageSize = returnPageSize,
                    fromDate = returnFromDate,
                    toDate = returnToDate
                });
            }

            var client = await _context.Clients.FindAsync(transaction.ClientId);
            ViewBag.ClientName = client?.Name ?? "";
            ViewBag.ActiveStep = formSection == "meal" ? "meal" : "accounts";
            ViewBag.ReturnPage = returnPage;
            ViewBag.ReturnPageSize = returnPageSize;
            ViewBag.ReturnFromDate = returnFromDate;
            ViewBag.ReturnToDate = returnToDate;
            ViewBag.FarmMealBatchId = formSection == "meal"
                ? await _context.Transactions
                    .Where(item => item.Id == id)
                    .Select(item => item.FarmMealBatchId)
                    .FirstOrDefaultAsync()
                : null;
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
        public async Task<IActionResult> Delete(
            int id,
            int returnPage = 1,
            int returnPageSize = 10,
            DateTime? returnFromDate = null,
            DateTime? returnToDate = null)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                transaction.IsDeleted = true; // Soft delete
                await SyncTreasuryEntriesForTransactionAsync(transaction);
                var farmMealBatchId = transaction.FarmMealBatchId;
                if (farmMealBatchId.HasValue)
                {
                    var batch = await _context.FarmMealBatches
                        .Include(item => item.Transactions)
                        .FirstOrDefaultAsync(item => item.Id == farmMealBatchId.Value);
                    if (batch == null)
                        return NotFound();

                    await RefreshFarmMealBatchAsync(batch);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الوجبة بنجاح";
                if (farmMealBatchId.HasValue)
                    return RedirectToAction(nameof(FarmMealDetails), new { id = farmMealBatchId.Value });
                return RedirectToAction("Profile", "Clients", new
                {
                    id = transaction.ClientId,
                    page = returnPage,
                    pageSize = returnPageSize,
                    fromDate = returnFromDate,
                    toDate = returnToDate
                });
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

            var invoice = new InvoiceViewModel
            {
                Transaction = transaction,
                MealTransactions = HasMealData(transaction)
                    ? new List<Transaction> { transaction }
                    : new List<Transaction>()
            };

            var whatsappNumber = NormalizeEgyptianPhoneNumber(transaction.Client.Phone);
            ViewBag.WhatsAppUrl = whatsappNumber == null
                ? null
                : $"https://wa.me/{whatsappNumber}?text={Uri.EscapeDataString(BuildInvoiceMessage(invoice, transaction.Client))}";

            return View(invoice);
        }

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

        private static bool IsOldBalancePasswordValid(string? password) =>
            string.Equals(password?.Trim(), OldBalanceEditPassword, StringComparison.Ordinal);

        private static DateTime NormalizeEntryDate(DateTime date, DateTime? fallback = null) =>
            date == default
                ? (fallback ?? DateTime.Today).Date
                : date.Date;

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
            var culture = CultureInfo.InvariantCulture;
            string Amount(decimal value) => value.ToString("#,##0.####", culture);

            var lines = new List<string>
            {
                "فاتورة حركة - بيرفكت",
                $"العميل: {client.Name}",
                $"التاريخ: {transaction.Date:yyyy/MM/dd}"
            };

            void AddAmount(string label, decimal value)
            {
                if (value != 0)
                    lines.Add($"{label}: {Amount(value)}");
            }

            void AddText(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    lines.Add($"{label}: {value.Trim()}");
            }

            lines.Add($"الرصيد قبل الحركة: {Amount(invoice.OpeningBalance)}");

            foreach (var meal in invoice.MealTransactions)
            {
                AddText("نوع الوجبة", meal.MealType);
                AddAmount("القائم", meal.GrossWeight);
                AddAmount("الفارغ", meal.EmptyWeight);
                AddAmount("الصافي", meal.NetWeight);
                AddAmount("سعر الكيلو", meal.PricePerKilo);
                AddAmount("إجمالي الوجبة", meal.MealTotal);
                AddText("ملاحظات", meal.GeneralNotes);
            }

            AddAmount("الإضافة", invoice.AdditionTotal);
            AddText("ملاحظات الإضافة", invoice.AdditionNotes);
            AddAmount("الخصم", invoice.DiscountTotal);
            AddText("ملاحظات الخصم", invoice.DiscountNotes);
            AddAmount("التحصيل النقدي", invoice.CashCollectionTotal);
            AddAmount("التحصيل الإلكتروني", invoice.ElectronicCollectionTotal);
            lines.Add($"الرصيد بعد الحركة: {Amount(invoice.EndingBalance)}");

            return string.Join(Environment.NewLine, lines);
        }

        private async Task SyncTreasuryEntriesForTransactionAsync(Transaction transaction)
        {
            var existingEntries = await _context.TreasuryEntries
                .Where(e => e.TransactionId == transaction.Id)
                .ToListAsync();

            if (transaction.IsDeleted)
            {
                if (existingEntries.Count > 0)
                {
                    _context.TreasuryEntries.RemoveRange(existingEntries);
                    await _context.SaveChangesAsync();
                }
                return;
            }

            if (existingEntries.Count > 0)
            {
                _context.TreasuryEntries.RemoveRange(existingEntries);
            }

            var clientName = transaction.Client?.Name;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                var client = await _context.Clients.FindAsync(transaction.ClientId);
                clientName = client?.Name ?? "";
            }

            var note = !string.IsNullOrWhiteSpace(transaction.GeneralNotes)
                ? $" ({transaction.GeneralNotes.Trim()})"
                : "";

            if (transaction.CashCollection > 0)
            {
                var cashAccount = transaction.CashTreasuryAccount ?? TreasuryAccount.HajjImamCash;
                _context.TreasuryEntries.Add(new TreasuryEntry
                {
                    Account = cashAccount,
                    Incoming = transaction.CashCollection,
                    Outgoing = 0,
                    Date = transaction.Date.Date,
                    Statement = $"تحصيل نقدي - العميل: {clientName}{note}",
                    TransactionId = transaction.Id
                });
            }

            if (transaction.ElectronicCollection > 0)
            {
                var elecAccount = transaction.ElectronicTreasuryAccount ?? TreasuryAccount.HajjImamVodafoneCash;
                _context.TreasuryEntries.Add(new TreasuryEntry
                {
                    Account = elecAccount,
                    Incoming = transaction.ElectronicCollection,
                    Outgoing = 0,
                    Date = transaction.Date.Date,
                    Statement = $"تحصيل إلكتروني - العميل: {clientName}{note}",
                    TransactionId = transaction.Id
                });
            }

            await _context.SaveChangesAsync();
        }

        private bool TransactionExists(int id)
        {
            return _context.Transactions.Any(e => e.Id == id);
        }
    }
}
