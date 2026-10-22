using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public enum TreasuryAccount
    {
        AbuMazenCash,
        HajjImamCash,
        AbuMazenVodafoneCash,
        AbuMazenInstaPay,
        HajjImamVodafoneCash,
        HajjImamInstaPay,
        General
    }

    public class TreasuryEntry
    {
        public int Id { get; set; }

        public TreasuryAccount Account { get; set; }

        [Required]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal Incoming { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Outgoing { get; set; }

        public DateTime Date { get; set; }

        public Guid? TransferId { get; set; }
    }
}
