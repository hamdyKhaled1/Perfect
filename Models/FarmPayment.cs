using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models;

public class FarmPayment
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string FarmName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    [Required]
    [StringLength(500)]
    public string Statement { get; set; } = string.Empty;

    public int? TreasuryEntryId { get; set; }

    [ForeignKey(nameof(TreasuryEntryId))]
    public virtual TreasuryEntry? TreasuryEntry { get; set; }
}
