using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class Client
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [Display(Name = "اسم العميل")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "رقم التليفون")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// عميل التلاجة: يمثل حساب كل عملاء الاستربس داخل قائمة العملاء.
        /// </summary>
        [Display(Name = "تلاجة")]
        public bool IsFridge { get; set; }

        /// <summary>
        /// الزيادة الافتراضية على سعر المزرعة، تظهر تلقائياً في صفحة إدخال الوجبات الجماعي.
        /// </summary>
        [Display(Name = "الزيادة على سعر المزرعة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultPriceIncrement { get; set; }

        /// <summary>
        /// الزيادة الافتراضية على سعر البانيه، تظهر تلقائياً في صفحة إدخال الوجبات الجماعي.
        /// </summary>
        [Display(Name = "زيادة سعر البانيه")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultPanehPriceIncrement { get; set; }

        // Navigation property
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        /// <summary>حركات التلاجة (تكون مستخدمة فقط لعميل التلاجة).</summary>
        public virtual ICollection<FridgeEntry> FridgeEntries { get; set; } = new List<FridgeEntry>();

        // Computed property - total due across all transactions
        [Display(Name = "إجمالي ما عليه")]
        public decimal TotalDue => ClientBalanceCalculator.Calculate(Transactions);
    }
}
