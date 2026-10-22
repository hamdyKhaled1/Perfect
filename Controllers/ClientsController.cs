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
            string? sortDirection,
            int page = 1,
            int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
            sortBy = sortBy is "TotalDue" or "LastTransaction" ? sortBy : "Name";
            sortDirection = sortDirection == "desc" ? "desc" : "asc";

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
                TotalDue = c.Transactions
                    .Where(t => !t.IsDeleted)
                    .Sum(t => (decimal?)(
                        (t.GrossWeight - t.EmptyWeight) * t.PricePerKilo +
                        t.OldBalance - t.CashCollection - t.ElectronicCollection - t.DiscountAmount)) ?? 0,
                TransactionCount = c.Transactions.Count(t => !t.IsDeleted),
                LastTransactionDate = c.Transactions
                    .Where(t => !t.IsDeleted)
                    .OrderByDescending(t => t.Date)
                    .Select(t => (DateTime?)t.Date)
                    .FirstOrDefault()
            });

            var totalCount = await clientsQuery.CountAsync();
            var totalDue = await clientQuery.SumAsync(c => (decimal?)c.TotalDue) ?? 0;
            var totalTransactions = await clientQuery.SumAsync(c => (int?)c.TransactionCount) ?? 0;
            page = totalCount == 0 ? 1 : Math.Min(page, (int)Math.Ceiling(totalCount / (double)pageSize));

            var orderedQuery = (sortBy, sortDirection) switch
            {
                ("TotalDue", "asc") => clientQuery.OrderBy(c => c.TotalDue).ThenBy(c => c.Id),
                ("TotalDue", "desc") => clientQuery.OrderByDescending(c => c.TotalDue).ThenBy(c => c.Id),
                ("LastTransaction", "asc") => clientQuery.OrderBy(c => c.LastTransactionDate).ThenBy(c => c.Id),
                ("LastTransaction", "desc") => clientQuery.OrderByDescending(c => c.LastTransactionDate).ThenBy(c => c.Id),
                ("Name", "desc") => clientQuery.OrderByDescending(c => c.Name).ThenBy(c => c.Id),
                _ => clientQuery.OrderBy(c => c.Name).ThenBy(c => c.Id)
            };
            var clients = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new ClientFilterViewModel
            {
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDirection = sortDirection,
                Clients = clients,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalDue = totalDue,
                TotalTransactions = totalTransactions
            };

            return View(viewModel);
        }

        // GET: Clients/ExportExcel - تصدير بيانات العملاء والوجبات
        public async Task<IActionResult> ExportExcel()
        {
            var clients = await _context.Clients
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Include(c => c.Transactions
                    .Where(t => !t.IsDeleted)
                    .OrderBy(t => t.Date))
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
                    .OrderBy(t => t.Date))
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
                    worksheet.Cell(row, 14).Value = transaction.FarmPrice;
                    worksheet.Cell(row, 15).Value = transaction.ExtraField;
                    worksheet.Cell(row, 16).Value = transaction.PricePerKilo;
                    worksheet.Cell(row, 17).Value = transaction.MealTotal;
                    worksheet.Cell(row, 18).Value = transaction.TotalDue;
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
        public async Task<IActionResult> Profile(int? id, DateTime? fromDate, DateTime? toDate)
        {
            if (id == null)
                return NotFound();

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
                return NotFound();

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

            var viewModel = new ClientProfileViewModel
            {
                Client = client,
                Transactions = await transactionsQuery.OrderByDescending(t => t.Date).ToListAsync(),
                OverallTotalDue = await _context.Transactions
                    .Where(t => t.ClientId == client.Id && !t.IsDeleted)
                    .SumAsync(t => (decimal?)(
                        (t.GrossWeight - t.EmptyWeight) * t.PricePerKilo +
                        t.OldBalance - t.CashCollection - t.ElectronicCollection +
                        t.AdditionAmount - t.DiscountAmount)) ?? 0,
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(viewModel);
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
