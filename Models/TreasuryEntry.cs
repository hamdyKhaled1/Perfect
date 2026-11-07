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
        General,
        MohamedAbdelrahmanCash,
        MohamedAbdelrahmanVodafoneCash,
        MohamedAbdelrahmanInstaPay
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

        public int? TransactionId { get; set; }

        [ForeignKey(nameof(TransactionId))]
        public virtual Transaction? Transaction { get; set; }

        public static string GetAccountName(TreasuryAccount account) => account switch
        {
            TreasuryAccount.AbuMazenCash => "خزنة أبو مازن - نقدي",
            TreasuryAccount.HajjImamCash => "خزنة الحاج إمام - نقدي",
            TreasuryAccount.AbuMazenVodafoneCash => "فودافون كاش - أبو مازن",
            TreasuryAccount.AbuMazenInstaPay => "إنستا باي - أبو مازن",
            TreasuryAccount.HajjImamVodafoneCash => "فودافون كاش - الحاج إمام",
            TreasuryAccount.HajjImamInstaPay => "إنستا باي - الحاج إمام",
            TreasuryAccount.General => "الخزنة العمومية",
            TreasuryAccount.MohamedAbdelrahmanCash => "خزنة محمد عبدالرحمن - نقدي",
            TreasuryAccount.MohamedAbdelrahmanVodafoneCash => "فودافون كاش - محمد عبدالرحمن",
            TreasuryAccount.MohamedAbdelrahmanInstaPay => "إنستا باي - محمد عبدالرحمن",
            _ => account.ToString()
        };
    }
}
