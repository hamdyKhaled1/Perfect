using System.ComponentModel.DataAnnotations;

namespace HagImamAccounts.Models;

public class StripCustomer
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    public virtual ICollection<StripInventoryEntry> Entries { get; set; } = new List<StripInventoryEntry>();
}
