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

        [NotMapped]
        public decimal FarmAccount => FarmPrice * FarmWeight;

        [NotMapped]
        public decimal RemainingFarmAccount => FarmAccount - FarmPaidAmount;

        [NotMapped]
        public decimal WeightDifference => FarmWeight - NetWeight;

        [NotMapped]
        public decimal Profit => WeightDifference * FarmPrice + MealTotal - FarmAccount;

        [NotMapped]
        public decimal FinalTotal => MealTotal - FarmAccount;

        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
