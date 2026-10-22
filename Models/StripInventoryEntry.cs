using System.ComponentModel.DataAnnotations;

namespace HagImamAccounts.Models;

public enum StripCartonType
{
    FiveKilogram,
    NineHundredGram
}

public class StripInventoryEntry
{
    public int Id { get; set; }

    public StripCartonType CartonType { get; set; }

    [Required]
    [StringLength(200)]
    public string ReceivedBy { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Statement { get; set; } = string.Empty;

    public int Incoming { get; set; }

    public int Outgoing { get; set; }

    public DateTime Date { get; set; }
}
