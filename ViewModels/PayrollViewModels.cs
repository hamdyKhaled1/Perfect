using System.ComponentModel.DataAnnotations;

namespace HagImamAccounts.ViewModels
{
    public class PayrollMonthViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<PayrollMonthRowViewModel> Rows { get; set; } = new();

        public string MonthName => Month is >= 1 and <= 12 ? Models.PayrollEntry.MonthNames[Month - 1] : Month.ToString();
        public string MonthLabel => $"{MonthName} {Year}";
        public DateTime MonthStart => new(Year, Month, 1);
        public DateTime MonthEnd => MonthStart.AddMonths(1).AddDays(-1);

        public decimal TotalBaseSalary => Rows.Sum(row => row.PayrollEntry?.BaseSalary ?? row.Employee.MonthlySalary);
        public decimal TotalAbsenceDeduction => Rows.Sum(row => row.PayrollEntry?.AbsenceDeduction ?? 0);
        public decimal TotalAdvancesDeduction => Rows.Sum(row => row.PayrollEntry?.AdvancesDeduction ?? 0);
        public decimal TotalOtherDeductions => Rows.Sum(row => row.PayrollEntry?.OtherDeductions ?? 0);
        public decimal TotalBonus => Rows.Sum(row => row.PayrollEntry?.Bonus ?? 0);
        public decimal TotalNetSalary => Rows.Sum(row => row.PayrollEntry?.NetSalary ?? 0);
        public decimal TotalPaid => Rows.Where(row => row.PayrollEntry?.IsPaid == true).Sum(row => row.PayrollEntry!.NetSalary);
        public decimal TotalPending => TotalNetSalary - TotalPaid;
        public int RecordedCount => Rows.Count(row => row.PayrollEntry != null);
        public int PaidCount => Rows.Count(row => row.PayrollEntry?.IsPaid == true);
        public int PendingCount => Rows.Count(row => row.PayrollEntry == null || !row.PayrollEntry.IsPaid);
    }

    public class PayrollMonthRowViewModel
    {
        public Models.Employee Employee { get; set; } = null!;
        public Models.PayrollEntry? PayrollEntry { get; set; }
        public decimal UnpaidAdvancesTotal { get; set; }
        public int UnpaidAdvancesCount { get; set; }
    }

    public class PayrollEntryFormViewModel
    {
        public int? Id { get; set; }

        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal EmployeeMonthlySalary { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "الراتب الأساسي لا يمكن أن يكون سالبًا.")]
        [Display(Name = "الراتب الأساسي")]
        public decimal BaseSalary { get; set; }

        [Range(0, 31, ErrorMessage = "أيام الغياب يجب أن تكون بين 0 و31.")]
        [Display(Name = "أيام الغياب")]
        public int AbsenceDays { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "قيمة اليوم لا يمكن أن تكون سالبة.")]
        [Display(Name = "قيمة اليوم")]
        public decimal DailyRate { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "الخصومات لا يمكن أن تكون سالبة.")]
        [Display(Name = "خصومات أخرى")]
        public decimal OtherDeductions { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "المكافأة لا يمكن أن تكون سالبة.")]
        [Display(Name = "مكافأة / إضافات")]
        public decimal Bonus { get; set; }

        [StringLength(1000)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public bool IsPaid { get; set; }
        public DateTime? PaidDate { get; set; }

        public List<PayrollAdvanceItemViewModel> Advances { get; set; } = new();
        public List<int> SelectedAdvanceIds { get; set; } = new();

        public string MonthName => Month is >= 1 and <= 12 ? Models.PayrollEntry.MonthNames[Month - 1] : Month.ToString();
        public string MonthLabel => $"{MonthName} {Year}";

        public decimal AbsenceDeduction => Math.Round(AbsenceDays * DailyRate, 4);
        public decimal SelectedAdvancesTotal =>
            Math.Round(Advances.Where(advance => SelectedAdvanceIds.Contains(advance.Id)).Sum(advance => advance.Amount), 4);
        public decimal NetSalary => Math.Round(BaseSalary + Bonus - AbsenceDeduction - SelectedAdvancesTotal - OtherDeductions, 4);
    }

    public class PayrollAdvanceItemViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
        public bool IsSelected { get; set; }
    }

    public class PayrollAdvancesViewModel
    {
        public int? EmployeeId { get; set; }
        public bool OnlyUnpaid { get; set; }
        public List<Models.Employee> Employees { get; set; } = new();
        public List<Models.EmployeeAdvance> Advances { get; set; } = new();
        public decimal TotalUnpaid => Advances.Where(advance => !advance.IsDeducted).Sum(advance => advance.Amount);
        public decimal TotalDeducted => Advances.Where(advance => advance.IsDeducted).Sum(advance => advance.Amount);
    }

    public class PayrollAdvanceFormViewModel
    {
        public int? Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر الموظف.")]
        [Display(Name = "الموظف")]
        public int EmployeeId { get; set; }

        [Range(typeof(decimal), "0.0001", "9999999999999999", ErrorMessage = "قيمة السلفة يجب أن تكون أكبر من صفر.")]
        [Display(Name = "قيمة السلفة")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "التاريخ")]
        public DateTime Date { get; set; } = DateTime.Today;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public List<Models.Employee> Employees { get; set; } = new();
    }

    public class EmployeeCustodiesViewModel
    {
        public List<Models.EmployeeCustody> Custodies { get; set; } = new();
        public decimal TotalIssued => Custodies.Sum(custody => custody.Amount);
        public decimal TotalSpent => Custodies.Sum(custody => custody.Movements.Sum(movement => movement.Amount));
        public decimal TotalRemaining => TotalIssued - TotalSpent;
    }

    public class EmployeeCustodyFormViewModel
    {
        public int? Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر الموظف.")]
        public int EmployeeId { get; set; }

        [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "قيمة العهدة يجب أن تكون أكبر من صفر.")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "اكتب بيان العهدة.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        public Models.TreasuryAccount TreasuryAccount { get; set; } = Models.TreasuryAccount.AbuMazenCash;

        public List<Models.Employee> Employees { get; set; } = new();
    }

    public class EmployeeCustodyMovementFormViewModel
    {
        public int Id { get; set; }

        public int EmployeeCustodyId { get; set; }

        [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "قيمة الحركة يجب أن تكون أكبر من صفر.")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "اكتب بيان الحركة.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;
    }

    public class EmployeeCustodyDetailsViewModel
    {
        public Models.EmployeeCustody Custody { get; set; } = null!;
        public EmployeeCustodyMovementFormViewModel NewMovement { get; set; } = new();
        public decimal Spent => Custody.Movements.Sum(movement => movement.Amount);
        public decimal Remaining => Custody.Amount - Spent;
    }

    public class PayrollEmployeesViewModel
    {
        public List<PayrollEmployeeRowViewModel> Employees { get; set; } = new();
        public decimal TotalMonthlySalaries => Employees.Where(row => row.Employee.IsActive).Sum(row => row.Employee.MonthlySalary);
        public int ActiveCount => Employees.Count(row => row.Employee.IsActive);
        public decimal TotalUnpaidAdvances => Employees.Sum(row => row.UnpaidAdvances);
    }

    public class PayrollEmployeeRowViewModel
    {
        public Models.Employee Employee { get; set; } = null!;
        public decimal UnpaidAdvances { get; set; }
        public int PayrollMonthsCount { get; set; }
    }
}
