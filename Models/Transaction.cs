using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class Transaction : IValidatableObject
    {
        public int Id { get; set; }

        // Foreign key
        public int ClientId { get; set; }

        public int? FarmMealBatchId { get; set; }

        [Display(Name = "التاريخ")]
        public DateTime Date { get; set; } = DateTime.Now;

        // ==================== قسم الحسابات ====================

        /// <summary>
        /// فلوس عليه قديم - الرصيد السابق
        /// </summary>
        [Display(Name = "فلوس عليه قديم")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OldBalance { get; set; }

        /// <summary>
        /// تحصيل نقدي
        /// </summary>
        [Display(Name = "تحصيل نقدي")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CashCollection { get; set; }

        /// <summary>
        /// تحصيل إلكتروني
        /// </summary>
        [Display(Name = "تحصيل إلكتروني")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ElectronicCollection { get; set; }

        [Display(Name = "الإضافة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AdditionAmount { get; set; }

        [Display(Name = "ملاحظات الإضافة")]
        [StringLength(500)]
        public string? AdditionNotes { get; set; }

        /// <summary>
        /// الباقي بعد التحصيل = فلوس عليه قديم - (تحصيل نقدي + تحصيل إلكتروني)
        /// محسوب تلقائياً
        /// </summary>
        [Display(Name = "الباقي بعد التحصيل")]
        [NotMapped]
        public decimal RemainingAfterCollection =>
            OldBalance - (CashCollection + ElectronicCollection);

        /// <summary>
        /// الخصم
        /// </summary>
        [Display(Name = "الخصم")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// ملاحظات الخصم
        /// </summary>
        [Display(Name = "ملاحظات الخصم")]
        [StringLength(500)]
        public string? DiscountNotes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (AdditionAmount != 0 && string.IsNullOrWhiteSpace(AdditionNotes))
            {
                yield return new ValidationResult(
                    "ملاحظات الإضافة مطلوبة عند إدخال مبلغ إضافة.",
                    new[] { nameof(AdditionNotes) });
            }

            if (DiscountAmount > 0 && string.IsNullOrWhiteSpace(DiscountNotes))
            {
                yield return new ValidationResult(
                    "ملاحظات الخصم مطلوبة عند إدخال خصم.",
                    new[] { nameof(DiscountNotes) });
            }
        }

        /// <summary>
        /// الإجمالي = الباقي بعد التحصيل - الخصم
        /// محسوب تلقائياً
        /// </summary>
        [Display(Name = "الإجمالي")]
        [NotMapped]
        public decimal SubTotal =>
            RemainingAfterCollection + AdditionAmount - DiscountAmount;

        // ==================== قسم الوجبة ====================

        /// <summary>
        /// القائم - الوزن الإجمالي (يدوي)
        /// </summary>
        [Display(Name = "القائم")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossWeight { get; set; }

        [NotMapped]
        public string? GrossWeightExpression { get; set; }

        /// <summary>
        /// وزن الفارغ ويُطرح مباشرة من القائم
        /// </summary>
        [Display(Name = "الفارغ")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmptyWeight { get; set; }

        [NotMapped]
        public string? EmptyWeightExpression { get; set; }

        /// <summary>
        /// الصافي = القائم - الفارغ
        /// محسوب تلقائياً
        /// </summary>
        [Display(Name = "الصافي")]
        [NotMapped]
        public decimal NetWeight => GrossWeight - EmptyWeight;

        /// <summary>
        /// زيادة تضاف إلى سعر المزرعة لتحديد سعر الكيلو.
        /// </summary>
        [Display(Name = "الزيادة على سعر المزرعة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtraField { get; set; }

        [Display(Name = "سعر المزرعة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FarmPrice { get; set; }

        /// <summary>
        /// سعر الكيلو
        /// </summary>
        [Display(Name = "سعر الكيلو")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerKilo { get; set; }

        /// <summary>
        /// إجمالي الوجبة = الصافي × سعر الكيلو
        /// محسوب تلقائياً
        /// </summary>
        [Display(Name = "إجمالي الوجبة")]
        [NotMapped]
        public decimal MealTotal => NetWeight * PricePerKilo;

        /// <summary>
        /// إجمالي ما عليه = إجمالي الوجبة + الإجمالي (الباقي بعد التحصيل والخصومات)
        /// محسوب تلقائياً
        /// </summary>
        [Display(Name = "إجمالي ما عليه")]
        [NotMapped]
        public decimal TotalDue => MealTotal + SubTotal;

        /// <summary>
        /// ملاحظات عامة
        /// </summary>
        [Display(Name = "ملاحظات")]
        [StringLength(1000)]
        public string? GeneralNotes { get; set; }

        [Display(Name = "محذوف")]
        public bool IsDeleted { get; set; } = false;

        // Navigation property
        [ForeignKey("ClientId")]
        public virtual Client? Client { get; set; }

        [ForeignKey(nameof(FarmMealBatchId))]
        public virtual FarmMealBatch? FarmMealBatch { get; set; }
    }
}
