using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.ViewModels;

namespace HagImamAccounts.Controllers
{
    public class ClientsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClientsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Clients - الصفحة الرئيسية مع قائمة العملاء
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? sortBy,
            int page = 1,
            int pageSize = 10)
        {
            return await ClientList("Index", searchTerm, sortBy, page, pageSize);
        }

        // GET: Clients/Details - قائمة العملاء الخاصة بصفحة تفاصيل العملاء
        public async Task<IActionResult> Details(
            string? searchTerm,
            string? sortBy,
            int page = 1,
            int pageSize = 10)
        {
            return await ClientList("Details", searchTerm, sortBy, page, pageSize);
        }

        private async Task<IActionResult> ClientList(
            string actionName,
            string? searchTerm,
            string? sortBy,
            int page,
            int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
            sortBy = sortBy is "TotalDue" or "LastTransaction" ? sortBy : "Name";

            var clientsQuery = _context.Clients.Where(c => c.IsActive);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                clientsQuery = clientsQuery.Where(c =>
                    c.Name.Contains(searchTerm) ||
                    (c.Phone != null && c.Phone.Contains(searchTerm)));
            }

            var clientQuery = clientsQuery.Select(c => new ClientListViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                IsFridge = c.IsFridge,
                TotalDue = c.IsFridge
                    ? (c.FridgeEntries
                        .Where(entry => entry.Source != FridgeEntrySource.StripSale)
                        .Sum(f => (decimal?)(f.Outgoing - f.Incoming)) ?? 0)
                    : (c.Transactions
                        .Where(t => !t.IsDeleted)
                        .OrderBy(t => t.Id)
                        .Select(t => (decimal?)t.OldBalance)
                        .FirstOrDefault() ?? 0) +
                      (c.Transactions
                        .Where(t => !t.IsDeleted)
                        .Sum(t => (decimal?)(
                            (t.GrossWeight - t.EmptyWeight) * t.PricePerKilo +
                            t.AdditionAmount - t.CashCollection - t.ElectronicCollection -
                            t.DiscountAmount)) ?? 0),
                TransactionCount = c.IsFridge
                    ? c.FridgeEntries.Count(entry => entry.Source != FridgeEntrySource.StripSale)
                    : c.Transactions.Count(t => !t.IsDeleted),
                LastTransactionDate = c.IsFridge
                    ? c.FridgeEntries
                        .Where(entry => entry.Source != FridgeEntrySource.StripSale)
                        .OrderByDescending(f => f.Date)
                        .ThenByDescending(f => f.Id)
                        .Select(f => (DateTime?)f.Date)
                        .FirstOrDefault()
                    : c.Transactions
                        .Where(t => !t.IsDeleted)
                        .OrderByDescending(t => t.Date)
                        .Select(t => (DateTime?)t.Date)
                        .FirstOrDefault()
            });

            var totalCount = await clientsQuery.CountAsync();
            var totalDue = await clientQuery.SumAsync(c => (decimal?)c.TotalDue) ?? 0;
            var totalTransactions = await clientQuery.SumAsync(c => (int?)c.TransactionCount) ?? 0;
            page = totalCount == 0 ? 1 : Math.Min(page, (int)Math.Ceiling(totalCount / (double)pageSize));

            var orderedQuery = sortBy switch
            {
                "TotalDue" => clientQuery.OrderBy(c => c.TotalDue).ThenBy(c => c.Id),
                "LastTransaction" => clientQuery.OrderByDescending(c => c.LastTransactionDate).ThenBy(c => c.Id),
                _ => clientQuery.OrderBy(c => c.Name).ThenBy(c => c.Id)
            };
            var clients = await orderedQuery.ToListAsync();
            var collectionWarnings = new List<ClientCollectionWarningViewModel>();
            if (actionName == "Index")
            {
                var collectionCutoff = DateTime.Today.AddDays(-3);
                var collectionRecords = await _context.Clients
                    .AsNoTracking()
                    .Where(client => client.IsActive)
                    .Select(client => new ClientCollectionWarningViewModel
                    {
                        ClientId = client.Id,
                        Name = client.Name,
                        TotalDue = client.IsFridge
                            ? (client.FridgeEntries
                                .Where(entry => entry.Source != FridgeEntrySource.StripSale)
                                .Sum(entry => (decimal?)(entry.Outgoing - entry.Incoming)) ?? 0)
                            : (client.Transactions
                                .Where(transaction => !transaction.IsDeleted)
                                .OrderBy(transaction => transaction.Id)
                                .Select(transaction => (decimal?)transaction.OldBalance)
                                .FirstOrDefault() ?? 0) +
                              (client.Transactions
                                .Where(transaction => !transaction.IsDeleted)
                                .Sum(transaction => (decimal?)(
                                    (transaction.GrossWeight - transaction.EmptyWeight) * transaction.PricePerKilo +
                                    transaction.AdditionAmount - transaction.CashCollection -
                                    transaction.ElectronicCollection - transaction.DiscountAmount)) ?? 0),
                        LastCollectionDate = client.IsFridge
                            ? client.FridgeEntries
                                .Where(entry => entry.Source != FridgeEntrySource.StripSale && entry.Incoming > 0)
                                .Max(entry => (DateTime?)entry.Date)
                            : client.Transactions
                                .Where(transaction => !transaction.IsDeleted &&
                                    (transaction.CashCollection > 0 || transaction.ElectronicCollection > 0))
                                .Max(transaction => (DateTime?)transaction.Date)
                    })
                    .ToListAsync();

                collectionWarnings = collectionRecords
                    .Where(client => client.TotalDue > 0 &&
                        (!client.LastCollectionDate.HasValue ||
                        client.LastCollectionDate.Value.Date <= collectionCutoff)
                    )
                    .Select(client => new ClientCollectionWarningViewModel
                    {
                        ClientId = client.ClientId,
                        Name = client.Name,
                        LastCollectionDate = client.LastCollectionDate,
                        DaysSinceCollection = client.LastCollectionDate.HasValue
                            ? (DateTime.Today - client.LastCollectionDate.Value.Date).Days
                            : null
                    })
                    .OrderByDescending(client => client.DaysSinceCollection ?? int.MaxValue)
                    .ThenBy(client => client.Name)
                    .ToList();
            }

            var viewModel = new ClientFilterViewModel
            {
                SearchTerm = searchTerm,
                SortBy = sortBy,
                Clients = clients,
                CollectionWarnings = collectionWarnings,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalDue = totalDue,
                TotalTransactions = totalTransactions
            };

            ViewBag.ClientListAction = actionName;
            ViewData["Title"] = actionName == "Details" ? "تفاصيل العملاء" : "العملاء";
            return View("Index", viewModel);
        }

        // GET: Clients/ExportExcel - تصدير بيانات العملاء والوجبات
        public async Task<IActionResult> ExportExcel()
        {
            var clients = await _context.Clients
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Include(c => c.Transactions
                    .Where(t => !t.IsDeleted)
                    .OrderByDescending(t => t.Date))
                .OrderBy(c => c.Name)
                .ToListAsync();

            return CreateExcelExport(clients, $"حسابات_العملاء_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // GET: Clients/ExportClientExcel/5 - تصدير حساب عميل واحد
        public async Task<IActionResult> ExportClientExcel(int id)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == id && c.IsActive)
                .Include(c => c.Transactions
                    .Where(t => !t.IsDeleted)
                    .OrderByDescending(t => t.Date))
                .FirstOrDefaultAsync();

            if (client == null)
                return NotFound();

            return CreateExcelExport(
                new[] { client },
                $"حساب_{client.Name}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        private FileContentResult CreateExcelExport(
            IEnumerable<Client> clients,
            string fileName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("حسابات العملاء");
            worksheet.RightToLeft = true;

            var headers = new[]
            {
                "اسم العميل",
                "فلوس عليه قديم",
                "تحصيل نقدي",
                "تحصيل إلكتروني",
                "الإضافة",
                "ملاحظات الإضافة",
                "الباقي بعد التحصيل",
                "الخصم",
                "ملاحظات الخصم",
                "الإجمالي",
                "القائم",
                "الفارغ",
                "الصافي",
                "نوع الوجبة",
                "سعر المزرعة",
                "الزيادة على سعر المزرعة",
                "سعر الكيلو",
                "إجمالي الوجبة",
                "إجمالي ما عليه"
            };

            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            var row = 2;
            foreach (var client in clients)
            {
                if (client.Transactions.Count == 0)
                {
                    worksheet.Cell(row++, 1).Value = client.Name;
                    continue;
                }

                foreach (var transaction in client.Transactions)
                {
                    worksheet.Cell(row, 1).Value = client.Name;
                    worksheet.Cell(row, 2).Value = transaction.OldBalance;
                    worksheet.Cell(row, 3).Value = transaction.CashCollection;
                    worksheet.Cell(row, 4).Value = transaction.ElectronicCollection;
                    worksheet.Cell(row, 5).Value = transaction.AdditionAmount;
                    worksheet.Cell(row, 6).Value = transaction.AdditionNotes ?? string.Empty;
                    worksheet.Cell(row, 7).Value = transaction.RemainingAfterCollection;
                    worksheet.Cell(row, 8).Value = transaction.DiscountAmount;
                    worksheet.Cell(row, 9).Value = transaction.DiscountNotes ?? string.Empty;
                    worksheet.Cell(row, 10).Value = transaction.SubTotal;
                    worksheet.Cell(row, 11).Value = transaction.GrossWeight;
                    worksheet.Cell(row, 12).Value = transaction.EmptyWeight;
                    worksheet.Cell(row, 13).Value = transaction.NetWeight;
                    worksheet.Cell(row, 14).Value = transaction.MealType ?? string.Empty;
                    worksheet.Cell(row, 15).Value = transaction.FarmPrice;
                    worksheet.Cell(row, 16).Value = transaction.ExtraField;
                    worksheet.Cell(row, 17).Value = transaction.PricePerKilo;
                    worksheet.Cell(row, 18).Value = transaction.MealTotal;
                    worksheet.Cell(row, 19).Value = transaction.TotalDue;
                    row++;
                }
            }

            var lastRow = row - 1;
            var header = worksheet.Range(1, 1, 1, headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#198754");
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.SheetView.FreezeRows(1);
            worksheet.Range(1, 1, lastRow, headers.Length).SetAutoFilter();
            worksheet.Columns().AdjustToContents();
            worksheet.Column(1).Width = 28;
            worksheet.Column(6).Width = 35;
            worksheet.Column(6).Style.Alignment.WrapText = true;
            worksheet.Column(9).Width = 35;
            worksheet.Column(9).Style.Alignment.WrapText = true;
            worksheet.Column(14).Width = 20;
            if (lastRow > 1)
                worksheet.Range(2, 2, lastRow, headers.Length).Style.NumberFormat.Format = "#,##0";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // GET: Clients/Profile/5 - ملف العميل
        public async Task<IActionResult> Profile(
            int? id,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1,
            int pageSize = 10)
        {
            if (id == null)
                return NotFound();

            page = Math.Max(page, 1);
            pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
                return NotFound();

            if (client.IsFridge)
                return RedirectToAction("Fridge", "StripInventory");

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
                ModelState.AddModelError(string.Empty, "تاريخ النهاية يجب أن يكون بعد تاريخ البداية أو مساويًا له.");

            var transactionsQuery = _context.Transactions
                .Where(t => t.ClientId == client.Id && !t.IsDeleted);

            if (ModelState.IsValid)
            {
                if (fromDate.HasValue)
                    transactionsQuery = transactionsQuery.Where(t => t.Date >= fromDate.Value.Date);

                if (toDate.HasValue)
                    transactionsQuery = transactionsQuery.Where(t => t.Date < toDate.Value.Date.AddDays(1));
            }

            var allClientTransactions = await _context.Transactions
                .Where(t => t.ClientId == client.Id && !t.IsDeleted)
                .ToListAsync();
            var movements = BuildClientMovements(allClientTransactions);
            if (ModelState.IsValid)
            {
                if (fromDate.HasValue)
                    movements = movements.Where(movement => movement.Date >= fromDate.Value.Date).ToList();

                if (toDate.HasValue)
                    movements = movements.Where(movement => movement.Date < toDate.Value.Date.AddDays(1)).ToList();
            }

            // الأحدث أولًا في العرض، مع الحفاظ على احتساب الرصيد الجاري بترتيب تاريخي.
            movements = movements
                .OrderByDescending(movement => movement.Date)
                .ThenByDescending(movement => movement.TransactionId)
                .ToList();

            var totalCount = movements.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            movements = movements
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModel = new ClientProfileViewModel
            {
                Client = client,
                Transactions = await transactionsQuery
                    .OrderByDescending(t => t.Date)
                    .ThenByDescending(t => t.Id)
                    .ToListAsync(),
                Movements = movements,
                OverallTotalDue = ClientBalanceCalculator.Calculate(allClientTransactions),
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(viewModel);
        }

        private static List<ClientMovementViewModel> BuildClientMovements(
            IEnumerable<Transaction> transactions)
        {
            var orderedTransactions = transactions
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.Id)
                .ToList();
            if (orderedTransactions.Count == 0)
                return new List<ClientMovementViewModel>();

            var remainingBalance = orderedTransactions
                .OrderBy(transaction => transaction.Id)
                .First()
                .OldBalance;
            var movements = new List<ClientMovementViewModel>();

            foreach (var transaction in orderedTransactions)
            {
                var mealTotal = transaction.MealTotal;
                var amountPaid = transaction.CashCollection + transaction.ElectronicCollection;
                var amountDue = mealTotal + transaction.AdditionAmount - transaction.DiscountAmount;
                remainingBalance += amountDue - amountPaid;

                movements.Add(new ClientMovementViewModel
                {
                    TransactionId = transaction.Id,
                    Date = transaction.Date,
                    Statement = BuildMovementStatement(transaction),
                    NetWeight = transaction.NetWeight,
                    PricePerKilo = transaction.PricePerKilo,
                    MealTotal = mealTotal,
                    AmountDue = amountDue,
                    AmountPaid = amountPaid,
                    RemainingBalance = remainingBalance
                });
            }

            return movements;
        }

        private static string BuildMovementStatement(Transaction transaction)
        {
            var statements = new List<string>();
            var hasMeal = transaction.GrossWeight != 0 ||
                          transaction.EmptyWeight != 0 ||
                          transaction.FarmPrice != 0 ||
                          transaction.ExtraField != 0 ||
                          transaction.PricePerKilo != 0;
            if (hasMeal)
            {
                var mealStatement = string.IsNullOrWhiteSpace(transaction.MealType)
                    ? "وجبة"
                    : $"وجبة {transaction.MealType}";
                statements.Add(mealStatement);
            }
            else if (!string.IsNullOrWhiteSpace(transaction.GeneralNotes))
            {
                statements.Add(transaction.GeneralNotes.Trim());
            }
            else if (transaction.CashCollection != 0 || transaction.ElectronicCollection != 0)
            {
                statements.Add("تحصيل");
            }

            if (!string.IsNullOrWhiteSpace(transaction.GeneralNotes) && hasMeal)
                statements.Add(transaction.GeneralNotes.Trim());
            if (!string.IsNullOrWhiteSpace(transaction.AdditionNotes))
                statements.Add($"إضافة: {transaction.AdditionNotes.Trim()}");
            if (!string.IsNullOrWhiteSpace(transaction.DiscountNotes))
                statements.Add($"خصم: {transaction.DiscountNotes.Trim()}");

            return statements.Count == 0 ? "حركة حساب" : string.Join("، ", statements.Distinct());
        }

        // GET: Clients/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Clients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Phone,Notes")] Client client)
        {
            if (ModelState.IsValid)
            {
                client.CreatedAt = DateTime.Now;
                _context.Add(client);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"تم إضافة العميل '{client.Name}' بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(client);
        }

        // GET: Clients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var client = await _context.Clients.FindAsync(id);
            if (client == null)
                return NotFound();

            if (client.IsFridge)
            {
                TempData["Error"] = "بيانات التلاجة تُدار من صفحة التلاجة نفسها.";
                return RedirectToAction(nameof(Index));
            }

            return View(client);
        }

        // POST: Clients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Phone,Notes,IsActive")] Client client)
        {
            if (id != client.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingClient = await _context.Clients.FindAsync(id);
                    if (existingClient == null)
                        return NotFound();

                    if (existingClient.IsFridge)
                    {
                        TempData["Error"] = "بيانات التلاجة تُدار من صفحة التلاجة نفسها.";
                        return RedirectToAction(nameof(Index));
                    }

                    existingClient.Name = client.Name;
                    existingClient.Phone = client.Phone;
                    existingClient.Notes = client.Notes;
                    existingClient.IsActive = client.IsActive;

                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"تم تعديل بيانات العميل '{client.Name}' بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClientExists(client.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(client);
        }

        // POST: Clients/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                if (client.IsFridge)
                {
                    TempData["Error"] = "لا يمكن حذف التلاجة؛ فهي حساب كل عملاء الاستربس.";
                    return RedirectToAction(nameof(Index));
                }

                client.IsActive = false; // Soft delete
                await _context.SaveChangesAsync();
                TempData["Success"] = $"تم حذف العميل '{client.Name}' بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ClientExists(int id)
        {
            return _context.Clients.Any(e => e.Id == id);
        }
    }
}
