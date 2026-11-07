using HagImamAccounts.Data;
using HagImamAccounts.Models;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HagImamAccounts.Controllers
{
    public class PayrollController : Controller
    {
        private const decimal DaysPerMonth = 30m;
        private readonly ApplicationDbContext _context;

        public PayrollController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===================== مرتبات الشهر =====================

        public async Task<IActionResult> Index(int? year, int? month)
        {
            var today = DateTime.Today;
            var safeYear = year is >= 2000 and <= 2100 ? year.Value : today.Year;
            var safeMonth = month is >= 1 and <= 12 ? month.Value : today.Month;

            return View(await BuildMonthAsync(safeYear, safeMonth));
        }

        private async Task<PayrollMonthViewModel> BuildMonthAsync(int year, int month)
        {
            var monthEnd = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            var entries = await _context.PayrollEntries
                .AsNoTracking()
                .Where(entry => entry.Year == year && entry.Month == month)
                .ToListAsync();

            var employeeIdsWithEntry = entries.Select(entry => entry.EmployeeId).ToList();

            var employees = await _context.Employees
                .AsNoTracking()
                .Where(employee => employee.IsActive || employeeIdsWithEntry.Contains(employee.Id))
                .OrderBy(employee => employee.Name)
                .ToListAsync();

            var unpaidAdvances = await _context.EmployeeAdvances
                .AsNoTracking()
                .Where(advance => advance.PayrollEntryId == null && advance.Date <= monthEnd)
                .GroupBy(advance => advance.EmployeeId)
                .Select(group => new
                {
                    EmployeeId = group.Key,
                    Total = group.Sum(advance => advance.Amount),
                    Count = group.Count()
                })
                .ToListAsync();

            var entryByEmployee = entries.ToDictionary(entry => entry.EmployeeId);
            var advancesByEmployee = unpaidAdvances.ToDictionary(item => item.EmployeeId);

            return new PayrollMonthViewModel
            {
                Year = year,
                Month = month,
                Rows = employees.Select(employee =>
                {
                    entryByEmployee.TryGetValue(employee.Id, out var entry);
                    advancesByEmployee.TryGetValue(employee.Id, out var advances);
                    return new PayrollMonthRowViewModel
                    {
                        Employee = employee,
                        PayrollEntry = entry,
                        UnpaidAdvancesTotal = advances?.Total ?? 0,
                        UnpaidAdvancesCount = advances?.Count ?? 0
                    };
                }).ToList()
            };
        }

        public async Task<IActionResult> Entry(int employeeId, int year, int month)
        {
            if (month is < 1 or > 12 || year is < 2000 or > 2100)
                return BadRequest();

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == employeeId);

            if (employee == null)
                return NotFound();

            return View(await BuildEntryFormAsync(employee, year, month));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEntry(PayrollEntryFormViewModel model)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(item => item.Id == model.EmployeeId);

            if (employee == null)
                return NotFound();

            if (model.Month is < 1 or > 12)
                ModelState.AddModelError(string.Empty, "الشهر غير صحيح.");
            if (model.Year is < 2000 or > 2100)
                ModelState.AddModelError(string.Empty, "السنة غير صحيحة.");

            var entry = await _context.PayrollEntries
                .Include(item => item.Advances)
                .FirstOrDefaultAsync(item =>
                    item.EmployeeId == model.EmployeeId &&
                    item.Year == model.Year &&
                    item.Month == model.Month);

            if (entry?.IsPaid == true)
            {
                TempData["Error"] = "المرتب مصروف بالفعل. تراجع عن الصرف أولًا لتعديله.";
                return RedirectToAction(nameof(Index), new { year = model.Year, month = model.Month });
            }

            var selectedIds = model.SelectedAdvanceIds.Distinct().ToHashSet();
            var monthEnd = new DateTime(model.Year, model.Month, 1).AddMonths(1).AddDays(-1);
            var selectedAdvances = selectedIds.Count == 0
                ? new List<EmployeeAdvance>()
                : await _context.EmployeeAdvances
                    .Where(advance => advance.EmployeeId == model.EmployeeId && selectedIds.Contains(advance.Id))
                    .ToListAsync();

            var lateAdvance = selectedAdvances.FirstOrDefault(advance => advance.Date > monthEnd);
            if (lateAdvance != null)
            {
                ModelState.AddModelError(string.Empty,
                    $"السلفة بتاريخ {lateAdvance.Date:dd/MM/yyyy} بعد نهاية الشهر المحدد ولا يمكن خصمها منه.");
            }

            var alreadyDeducted = selectedAdvances.FirstOrDefault(advance =>
                advance.PayrollEntryId.HasValue && advance.PayrollEntryId != entry?.Id);
            if (alreadyDeducted != null)
            {
                ModelState.AddModelError(string.Empty,
                    "إحدى السلف المختارة مخصومة بالفعل في شهر آخر.");
            }

            if (!ModelState.IsValid)
                return View("Entry", await BuildEntryFormAsync(employee, model.Year, model.Month, model));

            if (entry == null)
            {
                entry = new PayrollEntry
                {
                    EmployeeId = model.EmployeeId,
                    Year = model.Year,
                    Month = model.Month
                };
                _context.PayrollEntries.Add(entry);
            }

            entry.BaseSalary = model.BaseSalary;
            entry.AbsenceDays = model.AbsenceDays;
            entry.DailyRate = model.DailyRate;
            entry.OtherDeductions = model.OtherDeductions;
            entry.Bonus = model.Bonus;
            entry.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

            // حفظ السجل أولًا حتى يتوفر له رقم يمكن ربط السلف به.
            await _context.SaveChangesAsync();

            var relatedAdvances = await _context.EmployeeAdvances
                .Where(advance => advance.EmployeeId == model.EmployeeId &&
                                  (selectedIds.Contains(advance.Id) || advance.PayrollEntryId == entry.Id))
                .ToListAsync();

            foreach (var advance in relatedAdvances)
            {
                advance.PayrollEntryId = selectedIds.Contains(advance.Id) ? entry.Id : null;
            }

            entry.AdvancesDeduction = relatedAdvances
                .Where(advance => advance.PayrollEntryId == entry.Id)
                .Sum(advance => advance.Amount);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم حفظ مرتب {employee.Name} لشهر {entry.MonthLabel}.";
            return RedirectToAction(nameof(Index), new { year = entry.Year, month = entry.Month });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEntry(int id, int year, int month)
        {
            var entry = await _context.PayrollEntries
                .Include(item => item.Advances)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (entry == null)
                return NotFound();

            if (entry.IsPaid)
            {
                TempData["Error"] = "لا يمكن حذف مرتب مصروف. تراجع عن الصرف أولًا.";
                return RedirectToAction(nameof(Index), new { year, month });
            }

            foreach (var advance in entry.Advances)
            {
                advance.PayrollEntryId = null;
            }

            _context.PayrollEntries.Remove(entry);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حذف سجل المرتب وإرجاع السلف المرتبطة به.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, int year, int month)
        {
            var entry = await _context.PayrollEntries
                .Include(item => item.Employee)
                .Include(item => item.ExpenseEntry)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (entry == null)
                return NotFound();

            if (entry.IsPaid)
            {
                TempData["Error"] = "المرتب مصروف بالفعل.";
                return RedirectToAction(nameof(Index), new { year, month });
            }

            var netSalary = entry.NetSalary;
            if (netSalary <= 0)
            {
                TempData["Error"] = "صافي المرتب صفر أو أقل، لا يمكن صرفه.";
                return RedirectToAction(nameof(Index), new { year, month });
            }

            var expense = new ExpenseEntry
            {
                Statement = $"مرتب {entry.Employee?.Name ?? "موظف"} - {entry.MonthLabel}",
                Outgoing = netSalary,
                Date = DateTime.Today
            };

            entry.ExpenseEntry = expense;
            entry.IsPaid = true;
            entry.PaidDate = DateTime.Today;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم صرف مرتب {entry.Employee?.Name} لشهر {entry.MonthLabel} وتسجيله في المصروفات.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unpay(int id, int year, int month)
        {
            var entry = await _context.PayrollEntries
                .Include(item => item.ExpenseEntry)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (entry == null)
                return NotFound();

            if (entry.ExpenseEntry != null)
            {
                _context.ExpenseEntries.Remove(entry.ExpenseEntry);
            }

            entry.ExpenseEntry = null;
            entry.IsPaid = false;
            entry.PaidDate = null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم التراجع عن صرف المرتب وحذف حركة المصروفات المرتبطة به.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        private async Task<PayrollEntryFormViewModel> BuildEntryFormAsync(
            Employee employee,
            int year,
            int month,
            PayrollEntryFormViewModel? posted = null)
        {
            var monthEnd = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            var entry = await _context.PayrollEntries
                .AsNoTracking()
                .Include(item => item.Advances)
                .FirstOrDefaultAsync(item =>
                    item.EmployeeId == employee.Id &&
                    item.Year == year &&
                    item.Month == month);

            var entryId = entry?.Id ?? 0;
            var attachedIds = entry?.Advances.Select(advance => advance.Id).ToHashSet() ?? new HashSet<int>();

            var availableAdvances = await _context.EmployeeAdvances
                .AsNoTracking()
                .Where(advance => advance.EmployeeId == employee.Id &&
                                  advance.Date <= monthEnd &&
                                  (advance.PayrollEntryId == null || advance.PayrollEntryId == entryId))
                .OrderByDescending(advance => advance.Date)
                .ThenByDescending(advance => advance.Id)
                .ToListAsync();

            var selectedIds = posted?.SelectedAdvanceIds?.ToHashSet()
                ?? (entry == null ? availableAdvances.Select(advance => advance.Id).ToHashSet() : attachedIds);

            var model = posted ?? new PayrollEntryFormViewModel
            {
                Id = entry?.Id,
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                EmployeeMonthlySalary = employee.MonthlySalary,
                Year = year,
                Month = month,
                BaseSalary = entry?.BaseSalary ?? employee.MonthlySalary,
                AbsenceDays = entry?.AbsenceDays ?? 0,
                DailyRate = entry?.DailyRate ?? Math.Round(employee.MonthlySalary / DaysPerMonth, 4),
                OtherDeductions = entry?.OtherDeductions ?? 0,
                Bonus = entry?.Bonus ?? 0,
                Notes = entry?.Notes,
                IsPaid = entry?.IsPaid ?? false,
                PaidDate = entry?.PaidDate
            };

            model.Id = entry?.Id;
            model.EmployeeId = employee.Id;
            model.EmployeeName = employee.Name;
            model.EmployeeMonthlySalary = employee.MonthlySalary;
            model.Year = year;
            model.Month = month;
            model.IsPaid = entry?.IsPaid ?? false;
            model.PaidDate = entry?.PaidDate;
            model.Advances = availableAdvances.Select(advance => new PayrollAdvanceItemViewModel
            {
                Id = advance.Id,
                Date = advance.Date,
                Amount = advance.Amount,
                Notes = advance.Notes,
                IsSelected = selectedIds.Contains(advance.Id)
            }).ToList();

            return model;
        }

        // ===================== الموظفون =====================

        public async Task<IActionResult> Employees()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(employee => employee.IsActive ? 0 : 1)
                .ThenBy(employee => employee.Name)
                .ToListAsync();

            var unpaidAdvances = await _context.EmployeeAdvances
                .AsNoTracking()
                .Where(advance => advance.PayrollEntryId == null)
                .GroupBy(advance => advance.EmployeeId)
                .Select(group => new { EmployeeId = group.Key, Total = group.Sum(advance => advance.Amount) })
                .ToListAsync();

            var payrollCounts = await _context.PayrollEntries
                .AsNoTracking()
                .GroupBy(entry => entry.EmployeeId)
                .Select(group => new { EmployeeId = group.Key, Count = group.Count() })
                .ToListAsync();

            var unpaidByEmployee = unpaidAdvances.ToDictionary(item => item.EmployeeId);
            var countByEmployee = payrollCounts.ToDictionary(item => item.EmployeeId);

            return View(new PayrollEmployeesViewModel
            {
                Employees = employees.Select(employee => new PayrollEmployeeRowViewModel
                {
                    Employee = employee,
                    UnpaidAdvances = unpaidByEmployee.TryGetValue(employee.Id, out var unpaid) ? unpaid.Total : 0,
                    PayrollMonthsCount = countByEmployee.TryGetValue(employee.Id, out var count) ? count.Count : 0
                }).ToList()
            });
        }

        public IActionResult CreateEmployee()
        {
            return View("EmployeeForm", new Employee());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(Employee model)
        {
            if (!ModelState.IsValid)
                return View("EmployeeForm", model);

            model.Name = model.Name.Trim();
            model.JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? null : model.JobTitle.Trim();
            model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            model.CreatedAt = DateTime.Now;

            _context.Employees.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم إضافة الموظف {model.Name} بنجاح.";
            return RedirectToAction(nameof(Employees));
        }

        public async Task<IActionResult> EditEmployee(int id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (employee == null)
                return NotFound();

            return View("EmployeeForm", employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee(Employee model)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(item => item.Id == model.Id);

            if (employee == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View("EmployeeForm", model);

            employee.Name = model.Name.Trim();
            employee.JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? null : model.JobTitle.Trim();
            employee.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            employee.MonthlySalary = model.MonthlySalary;
            employee.HireDate = model.HireDate;
            employee.IsActive = model.IsActive;
            employee.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم تعديل بيانات الموظف {employee.Name} بنجاح.";
            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEmployee(int id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(item => item.Id == id);

            if (employee == null)
                return NotFound();

            employee.IsActive = !employee.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = employee.IsActive
                ? $"تم تفعيل الموظف {employee.Name}."
                : $"تم إيقاف الموظف {employee.Name} عن العمل (بياناته محفوظة).";
            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(item => item.Id == id);

            if (employee == null)
                return NotFound();

            var hasPayroll = await _context.PayrollEntries.AnyAsync(entry => entry.EmployeeId == id);
            var hasAdvances = await _context.EmployeeAdvances.AnyAsync(advance => advance.EmployeeId == id);
            var hasCustodies = await _context.EmployeeCustodies.AnyAsync(custody => custody.EmployeeId == id);

            if (hasPayroll || hasAdvances || hasCustodies)
            {
                TempData["Error"] = "لا يمكن حذف موظف له سلف أو عهد أو مرتبات مسجلة. يمكنك إيقافه بدل الحذف.";
                return RedirectToAction(nameof(Employees));
            }

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم حذف الموظف {employee.Name}.";
            return RedirectToAction(nameof(Employees));
        }

        // ===================== العهد =====================

        private static readonly TreasuryAccount[] CustodyTreasuryAccounts =
        {
            TreasuryAccount.AbuMazenCash,
            TreasuryAccount.AbuMazenVodafoneCash
        };

        public async Task<IActionResult> Custodies()
        {
            var custodies = await _context.EmployeeCustodies
                .AsNoTracking()
                .Include(custody => custody.Employee)
                .Include(custody => custody.Movements)
                .OrderByDescending(custody => custody.Date)
                .ThenByDescending(custody => custody.Id)
                .ToListAsync();

            return View(new EmployeeCustodiesViewModel { Custodies = custodies });
        }

        public async Task<IActionResult> CreateCustody()
        {
            return View("CustodyForm", new EmployeeCustodyFormViewModel
            {
                Employees = await GetEmployeeOptionsAsync(includeInactive: false)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustody(EmployeeCustodyFormViewModel model)
        {
            if (!await _context.Employees.AnyAsync(employee => employee.Id == model.EmployeeId && employee.IsActive))
                ModelState.AddModelError(nameof(model.EmployeeId), "اختر موظفًا عاملًا.");
            if (!CustodyTreasuryAccounts.Contains(model.TreasuryAccount))
                ModelState.AddModelError(nameof(model.TreasuryAccount), "العهدة متاحة من خزنة أبو مازن النقدي أو فودافون كاش فقط.");

            if (!ModelState.IsValid)
            {
                model.Employees = await GetEmployeeOptionsAsync(includeInactive: false);
                return View("CustodyForm", model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var availableBalance = await _context.TreasuryEntries
                .Where(entry => entry.Account == model.TreasuryAccount)
                .SumAsync(entry => entry.Incoming - entry.Outgoing);
            var existingCustody = await _context.EmployeeCustodies
                .Include(custody => custody.TreasuryEntry)
                .Where(custody =>
                    custody.EmployeeId == model.EmployeeId &&
                    custody.TreasuryAccount == model.TreasuryAccount &&
                    custody.TreasuryEntry != null &&
                    custody.TreasuryEntry.Account == model.TreasuryAccount)
                .OrderByDescending(custody => custody.Date)
                .ThenByDescending(custody => custody.Id)
                .FirstOrDefaultAsync();

            if (model.Amount > availableBalance)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(
                    nameof(model.Amount),
                    $"الرصيد غير كافٍ في {TreasuryEntry.GetAccountName(model.TreasuryAccount)}. الرصيد الحالي: {availableBalance:#,##0.####}.");
                model.Employees = await GetEmployeeOptionsAsync(includeInactive: false);
                return View("CustodyForm", model);
            }

            var statement = model.Statement.Trim();
            if (existingCustody != null)
            {
                var combinedStatement = $"{existingCustody.Statement} | {model.Date:dd/MM/yyyy}: {statement}";
                if (combinedStatement.Length > 500)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(
                        nameof(model.Statement),
                        "تعذر دمج البيان؛ اختصر البيان الجديد أو عدّل بيان العهدة الحالية أولًا.");
                    model.Employees = await GetEmployeeOptionsAsync(includeInactive: false);
                    return View("CustodyForm", model);
                }

                if (existingCustody.Amount + model.Amount > 99999999999999.9999m)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(nameof(model.Amount), "إجمالي العهدة سيتجاوز الحد الأقصى المسموح.");
                    model.Employees = await GetEmployeeOptionsAsync(includeInactive: false);
                    return View("CustodyForm", model);
                }

                existingCustody.Amount += model.Amount;
                existingCustody.Statement = combinedStatement;
                existingCustody.TreasuryEntry!.Outgoing += model.Amount;
                var treasuryStatement = $"عهدة للموظف: {combinedStatement}";
                existingCustody.TreasuryEntry.Statement =
                    treasuryStatement[..Math.Min(treasuryStatement.Length, 500)];
            }
            else
            {
                var treasuryStatement = $"عهدة للموظف: {statement}";
                _context.EmployeeCustodies.Add(new EmployeeCustody
                {
                    EmployeeId = model.EmployeeId,
                    Amount = model.Amount,
                    Date = model.Date.Date,
                    Statement = statement,
                    TreasuryAccount = model.TreasuryAccount,
                    TreasuryEntry = new TreasuryEntry
                    {
                        Account = model.TreasuryAccount,
                        Statement = treasuryStatement[..Math.Min(treasuryStatement.Length, 500)],
                        Outgoing = model.Amount,
                        Date = model.Date.Date
                    }
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = existingCustody == null
                ? $"تم تسجيل العهدة وخصم {model.Amount:#,##0.####} من {TreasuryEntry.GetAccountName(model.TreasuryAccount)}."
                : $"تمت إضافة {model.Amount:#,##0.####} إلى عهدة الموظف وخصمها من {TreasuryEntry.GetAccountName(model.TreasuryAccount)}.";
            return RedirectToAction(nameof(Custodies));
        }

        public async Task<IActionResult> EditCustody(int id)
        {
            var custody = await _context.EmployeeCustodies
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
            if (custody == null)
                return NotFound();

            return View("CustodyForm", new EmployeeCustodyFormViewModel
            {
                Id = custody.Id,
                EmployeeId = custody.EmployeeId,
                Amount = custody.Amount,
                Date = custody.Date,
                Statement = custody.Statement,
                TreasuryAccount = custody.TreasuryAccount,
                Employees = await GetEmployeeOptionsAsync(includeInactive: true)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustody(EmployeeCustodyFormViewModel model)
        {
            var custody = await _context.EmployeeCustodies
                .Include(item => item.Movements)
                .Include(item => item.TreasuryEntry)
                .FirstOrDefaultAsync(item => item.Id == model.Id);
            if (custody == null)
                return NotFound();

            if (!await _context.Employees.AnyAsync(employee => employee.Id == model.EmployeeId))
                ModelState.AddModelError(nameof(model.EmployeeId), "اختر موظفًا مسجلًا.");
            if (!CustodyTreasuryAccounts.Contains(model.TreasuryAccount))
                ModelState.AddModelError(nameof(model.TreasuryAccount), "العهدة متاحة من خزنة أبو مازن النقدي أو فودافون كاش فقط.");

            var spent = custody.Movements.Sum(movement => movement.Amount);
            if (model.Amount < spent)
                ModelState.AddModelError(nameof(model.Amount), $"مبلغ العهدة لا يمكن أن يقل عن الحركات المسجلة ({spent:#,##0.####}).");

            if (!ModelState.IsValid)
            {
                model.Employees = await GetEmployeeOptionsAsync(includeInactive: true);
                return View("CustodyForm", model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var availableBalance = await _context.TreasuryEntries
                .Where(entry => entry.Account == model.TreasuryAccount)
                .SumAsync(entry => entry.Incoming - entry.Outgoing);
            if (custody.TreasuryEntry?.Account == model.TreasuryAccount)
                availableBalance += custody.TreasuryEntry.Outgoing;

            if (model.Amount > availableBalance)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(
                    nameof(model.Amount),
                    $"الرصيد غير كافٍ في {TreasuryEntry.GetAccountName(model.TreasuryAccount)}. الرصيد المتاح بعد استبعاد العهدة الحالية: {availableBalance:#,##0.####}.");
                model.Employees = await GetEmployeeOptionsAsync(includeInactive: true);
                return View("CustodyForm", model);
            }

            custody.EmployeeId = model.EmployeeId;
            custody.Amount = model.Amount;
            custody.Date = model.Date.Date;
            custody.Statement = model.Statement.Trim();
            custody.TreasuryAccount = model.TreasuryAccount;

            var treasuryStatement = $"عهدة للموظف: {custody.Statement}";
            if (custody.TreasuryEntry == null)
            {
                custody.TreasuryEntry = new TreasuryEntry
                {
                    Account = model.TreasuryAccount,
                    Outgoing = model.Amount,
                    Date = model.Date.Date,
                    Statement = treasuryStatement[..Math.Min(treasuryStatement.Length, 500)]
                };
            }
            else
            {
                custody.TreasuryEntry.Account = model.TreasuryAccount;
                custody.TreasuryEntry.Outgoing = model.Amount;
                custody.TreasuryEntry.Date = model.Date.Date;
                custody.TreasuryEntry.Statement = treasuryStatement[..Math.Min(treasuryStatement.Length, 500)];
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم تعديل العهدة وتحديث حركة الخزنة المرتبطة بها.";
            return RedirectToAction(nameof(Custodies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustody(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var custody = await _context.EmployeeCustodies
                .Include(item => item.Movements)
                .Include(item => item.TreasuryEntry)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (custody == null)
                return NotFound();

            if (custody.Movements.Count > 0)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "لا يمكن حذف عهدة عليها حركات صرف مسجلة. راجع تفاصيل الحركات أولًا.";
                return RedirectToAction(nameof(Custodies));
            }

            if (custody.TreasuryEntry != null)
                _context.TreasuryEntries.Remove(custody.TreasuryEntry);
            _context.EmployeeCustodies.Remove(custody);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم حذف العهدة وحركة الخصم المرتبطة بها.";
            return RedirectToAction(nameof(Custodies));
        }

        public async Task<IActionResult> CustodyDetails(int id)
        {
            var custody = await _context.EmployeeCustodies
                .AsNoTracking()
                .Include(item => item.Employee)
                .Include(item => item.Movements.OrderByDescending(movement => movement.Date).ThenByDescending(movement => movement.Id))
                .FirstOrDefaultAsync(item => item.Id == id);
            if (custody == null)
                return NotFound();

            return View(new EmployeeCustodyDetailsViewModel
            {
                Custody = custody,
                NewMovement = new EmployeeCustodyMovementFormViewModel
                {
                    EmployeeCustodyId = custody.Id,
                    Date = DateTime.Today
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCustodyMovement(
            [Bind(Prefix = "NewMovement")] EmployeeCustodyMovementFormViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var custody = await _context.EmployeeCustodies
                .Include(item => item.Employee)
                .Include(item => item.Movements)
                .FirstOrDefaultAsync(item => item.Id == model.EmployeeCustodyId);
            if (custody == null)
                return NotFound();

            var remaining = custody.Amount - custody.Movements.Sum(movement => movement.Amount);
            if (model.Amount > remaining)
                ModelState.AddModelError("NewMovement.Amount", $"المبلغ أكبر من المتبقي في العهدة ({remaining:#,##0.####}).");

            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync();
                custody.Movements = custody.Movements
                    .OrderByDescending(movement => movement.Date)
                    .ThenByDescending(movement => movement.Id)
                    .ToList();
                return View("CustodyDetails", new EmployeeCustodyDetailsViewModel
                {
                    Custody = custody,
                    NewMovement = model
                });
            }

            _context.EmployeeCustodyMovements.Add(new EmployeeCustodyMovement
            {
                EmployeeCustodyId = custody.Id,
                Amount = model.Amount,
                Date = model.Date.Date,
                Statement = model.Statement.Trim()
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = $"تم تسجيل الحركة وخصم {model.Amount:#,##0.####} من عهدة {custody.Employee?.Name}.";
            return RedirectToAction(nameof(CustodyDetails), new { id = custody.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustodyMovement(
            [Bind(Prefix = "Movement")] EmployeeCustodyMovementFormViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var movement = await _context.EmployeeCustodyMovements
                .FirstOrDefaultAsync(item => item.Id == model.Id && item.EmployeeCustodyId == model.EmployeeCustodyId);
            if (movement == null)
                return NotFound();

            var custody = await _context.EmployeeCustodies
                .Include(item => item.Movements)
                .FirstOrDefaultAsync(item => item.Id == model.EmployeeCustodyId);
            if (custody == null)
                return NotFound();

            var availableAmount = custody.Amount -
                custody.Movements.Where(item => item.Id != movement.Id).Sum(item => item.Amount);
            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = string.Join(" ", ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage));
                return RedirectToAction(nameof(CustodyDetails), new { id = custody.Id });
            }

            if (model.Amount > availableAmount)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"المبلغ أكبر من المتاح لهذه الحركة ({availableAmount:#,##0.####}).";
                return RedirectToAction(nameof(CustodyDetails), new { id = custody.Id });
            }

            movement.Amount = model.Amount;
            movement.Date = model.Date.Date;
            movement.Statement = model.Statement.Trim();
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم تعديل حركة العهدة بنجاح.";
            return RedirectToAction(nameof(CustodyDetails), new { id = custody.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustodyMovement(int id, int custodyId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var movement = await _context.EmployeeCustodyMovements
                .FirstOrDefaultAsync(item => item.Id == id && item.EmployeeCustodyId == custodyId);
            if (movement == null)
                return NotFound();

            _context.EmployeeCustodyMovements.Remove(movement);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم حذف حركة العهدة وإعادة المبلغ إلى رصيدها.";
            return RedirectToAction(nameof(CustodyDetails), new { id = custodyId });
        }

        // ===================== السلف =====================

        public async Task<IActionResult> Advances(int? employeeId, bool onlyUnpaid = false)
        {
            var query = _context.EmployeeAdvances
                .AsNoTracking()
                .Include(advance => advance.Employee)
                .Include(advance => advance.PayrollEntry)
                .AsQueryable();

            if (employeeId.HasValue)
                query = query.Where(advance => advance.EmployeeId == employeeId.Value);
            if (onlyUnpaid)
                query = query.Where(advance => advance.PayrollEntryId == null);

            return View(new PayrollAdvancesViewModel
            {
                EmployeeId = employeeId,
                OnlyUnpaid = onlyUnpaid,
                Employees = await GetEmployeeOptionsAsync(includeInactive: true),
                Advances = await query
                    .OrderByDescending(advance => advance.Date)
                    .ThenByDescending(advance => advance.Id)
                    .ToListAsync()
            });
        }

        public async Task<IActionResult> CreateAdvance(int? employeeId)
        {
            var model = new PayrollAdvanceFormViewModel
            {
                EmployeeId = employeeId ?? 0,
                Employees = await GetEmployeeOptionsAsync(includeInactive: false)
            };
            return View("AdvanceForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdvance(PayrollAdvanceFormViewModel model)
        {
            if (!await _context.Employees.AnyAsync(employee => employee.Id == model.EmployeeId))
                ModelState.AddModelError(nameof(model.EmployeeId), "اختر موظفًا صحيحًا.");

            if (!ModelState.IsValid)
            {
                model.Employees = await GetEmployeeOptionsAsync(includeInactive: false);
                return View("AdvanceForm", model);
            }

            _context.EmployeeAdvances.Add(new EmployeeAdvance
            {
                EmployeeId = model.EmployeeId,
                Amount = model.Amount,
                Date = model.Date.Date,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تسجيل السلفة بنجاح.";
            return RedirectToAction(nameof(Advances), new { employeeId = model.EmployeeId });
        }

        public async Task<IActionResult> EditAdvance(int id)
        {
            var advance = await _context.EmployeeAdvances
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (advance == null)
                return NotFound();

            if (advance.PayrollEntryId.HasValue)
            {
                TempData["Error"] = "لا يمكن تعديل سلفة مخصومة في مرتب شهر. تراجع عن المرتب أولًا.";
                return RedirectToAction(nameof(Advances), new { employeeId = advance.EmployeeId });
            }

            return View("AdvanceForm", new PayrollAdvanceFormViewModel
            {
                Id = advance.Id,
                EmployeeId = advance.EmployeeId,
                Amount = advance.Amount,
                Date = advance.Date,
                Notes = advance.Notes,
                Employees = await GetEmployeeOptionsAsync(includeInactive: true)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAdvance(PayrollAdvanceFormViewModel model)
        {
            var advance = await _context.EmployeeAdvances
                .FirstOrDefaultAsync(item => item.Id == model.Id);

            if (advance == null)
                return NotFound();

            if (advance.PayrollEntryId.HasValue)
            {
                TempData["Error"] = "لا يمكن تعديل سلفة مخصومة في مرتب شهر. تراجع عن المرتب أولًا.";
                return RedirectToAction(nameof(Advances), new { employeeId = advance.EmployeeId });
            }

            if (!await _context.Employees.AnyAsync(employee => employee.Id == model.EmployeeId))
                ModelState.AddModelError(nameof(model.EmployeeId), "اختر موظفًا صحيحًا.");

            if (!ModelState.IsValid)
            {
                model.Employees = await GetEmployeeOptionsAsync(includeInactive: true);
                return View("AdvanceForm", model);
            }

            advance.EmployeeId = model.EmployeeId;
            advance.Amount = model.Amount;
            advance.Date = model.Date.Date;
            advance.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تعديل السلفة بنجاح.";
            return RedirectToAction(nameof(Advances), new { employeeId = advance.EmployeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdvance(int id)
        {
            var advance = await _context.EmployeeAdvances
                .FirstOrDefaultAsync(item => item.Id == id);

            if (advance == null)
                return NotFound();

            if (advance.PayrollEntryId.HasValue)
            {
                TempData["Error"] = "لا يمكن حذف سلفة مخصومة في مرتب شهر. تراجع عن المرتب أولًا.";
                return RedirectToAction(nameof(Advances), new { employeeId = advance.EmployeeId });
            }

            var employeeId = advance.EmployeeId;
            _context.EmployeeAdvances.Remove(advance);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف السلفة بنجاح.";
            return RedirectToAction(nameof(Advances), new { employeeId });
        }

        private async Task<List<Employee>> GetEmployeeOptionsAsync(bool includeInactive)
        {
            var query = _context.Employees.AsNoTracking().AsQueryable();
            if (!includeInactive)
                query = query.Where(employee => employee.IsActive);

            return await query.OrderBy(employee => employee.Name).ToListAsync();
        }
    }
}
