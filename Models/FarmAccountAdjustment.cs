using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models;

public enum FarmAccountAdjustmentType
{
    Addition,
    Discount
}

public class FarmAccountAdjustment
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string FarmName { get; set; } = string.Empty;

    public FarmAccountAdjustmentType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0.01", "99999999999999.99", ErrorMessage = "أدخل مبلغًا أكبر من صفر.")]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    [Required]
    [StringLength(500)]
    public string Statement { get; set; } = string.Empty;
}
