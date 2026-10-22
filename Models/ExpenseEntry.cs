using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class ExpenseEntry
    {
        public int Id { get; set; }

        [Required]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal Incoming { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Outgoing { get; set; }

        public DateTime Date { get; set; }

        public int? FarmMealBatchId { get; set; }

        [ForeignKey(nameof(FarmMealBatchId))]
        public virtual FarmMealBatch? FarmMealBatch { get; set; }
    }
}
