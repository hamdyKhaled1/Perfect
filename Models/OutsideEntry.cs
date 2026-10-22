using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class OutsideEntry
    {
        public int Id { get; set; }

        public int OutsideCustomerId { get; set; }

        [Required]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Incoming { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Taken { get; set; }

        public DateTime Date { get; set; }

        [ForeignKey(nameof(OutsideCustomerId))]
        public virtual OutsideCustomer? Customer { get; set; }
    }
}
