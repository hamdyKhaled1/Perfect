using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models;

public enum StripCartonType
{
    FiveKilogram,
    NineHundredGram
}

public enum StripEntryType
{
    Sale,
    Payment,
    StockIn,
    StockOut,
    Return
}

public class StripInventoryEntry
{
    public int Id { get; set; }

    public int? StripCustomerId { get; set; }

    public StripCartonType CartonType { get; set; }

    public StripEntryType EntryType { get; set; } = StripEntryType.Sale;

    [Required]
    [StringLength(200)]
    public string ReceivedBy { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Statement { get; set; } = string.Empty;

    public int Incoming { get; set; }

    public int Outgoing { get; set; }

    public int Cartons { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerCarton { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    public DateTime Date { get; set; }

    [NotMapped]
    public decimal Total => Cartons * PricePerCarton;

    public virtual StripCustomer? Customer { get; set; }
}
