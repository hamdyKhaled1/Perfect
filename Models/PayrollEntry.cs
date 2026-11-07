using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    /// <summary>
    /// مرتب موظف في شهر محدد، مع الخصومات والإضافات الخاصة بهذا الشهر.
    /// </summary>
    public class PayrollEntry
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }

        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Display(Name = "الشهر")]
        public int Month { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "الراتب الأساسي")]
        public decimal BaseSalary { get; set; }

        [Range(0, 31, ErrorMessage = "أيام الغياب يجب أن تكون بين 0 و31.")]
        [Display(Name = "أيام الغياب")]
        public int AbsenceDays { get; set; }

        /// <summary>
        /// قيمة اليوم الواحد، والافتراضي الراتب الأساسي ÷ 30.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "قيمة اليوم")]
        public decimal DailyRate { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "خصومات أخرى")]
        public decimal OtherDeductions { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "مكافأة / إضافات")]
        public decimal Bonus { get; set; }

        /// <summary>
        /// مجموع السلف المرتبطة بهذا الشهر (محفوظ لتسهيل التقارير).
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "السلف المخصومة")]
        public decimal AdvancesDeduction { get; set; }

        [StringLength(1000)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "تم الصرف")]
        public bool IsPaid { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاريخ الصرف")]
        public DateTime? PaidDate { get; set; }

        public virtual ExpenseEntry? ExpenseEntry { get; set; }

        public virtual ICollection<EmployeeAdvance> Advances { get; set; } = new List<EmployeeAdvance>();

        [NotMapped]
        public decimal AbsenceDeduction => Math.Round(AbsenceDays * DailyRate, 4);

        [NotMapped]
        public decimal TotalDeductions => AbsenceDeduction + AdvancesDeduction + OtherDeductions;

        [NotMapped]
        public decimal NetSalary => Math.Round(BaseSalary + Bonus - TotalDeductions, 4);

        public static readonly string[] MonthNames =
        {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
        };

        [NotMapped]
        public string MonthName => Month is >= 1 and <= 12 ? MonthNames[Month - 1] : Month.ToString();

        [NotMapped]
        public string MonthLabel => $"{MonthName} {Year}";
    }
}
