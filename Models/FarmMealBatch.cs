using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class FarmMealBatch
    {
        public int Id { get; set; }

        [Display(Name = "التاريخ")]
        public DateTime Date { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "اسم المزرعة")]
        public string FarmName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FarmWeight { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FarmPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetWeight { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal MealTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FarmPaidAmount { get; set; }

        /// <summary>
        /// إضافة على حساب المزرعة (تزيد المتبقي للمزرعة).
        /// </summary>
        [Display(Name = "إضافة على حساب المزرعة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FarmAdditionAmount { get; set; }

        [StringLength(500)]
        public string? FarmAdditionStatement { get; set; }

        /// <summary>
        /// خصم من حساب المزرعة (يقلل المتبقي للمزرعة).
        /// </summary>
        [Display(Name = "خصم من حساب المزرعة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FarmDiscountAmount { get; set; }

        [StringLength(500)]
        public string? FarmDiscountStatement { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousFarmBalance { get; set; }

        [NotMapped]
        public decimal CurrentFarmAccount => FarmPrice * FarmWeight;

        [NotMapped]
        public decimal FarmAccount => CurrentFarmAccount;

        [NotMapped]
        public decimal TotalFarmAccount => FarmAccount + PreviousFarmBalance + FarmAdditionAmount - FarmDiscountAmount;

        [NotMapped]
        public decimal RemainingFarmAccount => TotalFarmAccount - FarmPaidAmount;

        [NotMapped]
        public decimal WeightDifference => NetWeight - FarmWeight;

        [NotMapped]
        public decimal Profit => MealTotal - CurrentFarmAccount;

        [NotMapped]
        public decimal FinalTotal => MealTotal - CurrentFarmAccount;

        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
