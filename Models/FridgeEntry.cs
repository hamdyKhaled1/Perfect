using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models;

public enum FridgeEntrySource
{
    /// <summary>حركة يدوية من صفحة التلاجة.</summary>
    Manual = 0,

    /// <summary>بيع لأحد عملاء الاستربس (صادر).</summary>
    StripSale = 1,

    /// <summary>تحصيل من أحد عملاء الاستربس (وارد).</summary>
    StripPayment = 2
}

/// <summary>
/// دفتر التلاجة: كل حركة صادر أو وارد مع بيان، ويشمل تلقائيًا حركات عملاء الاستربس.
/// الرصيد (الباقي على عملاء الاستربس) = مجموع الصادر - مجموع الوارد.
/// </summary>
public class FridgeEntry
{
    public int Id { get; set; }

    /// <summary>عميل التلاجة في قائمة العملاء.</summary>
    public int ClientId { get; set; }

    public virtual Client? Client { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    [Required]
    [StringLength(500)]
    public string Statement { get; set; } = string.Empty;

    /// <summary>وارد: مبالغ دخلت التلاجة (تحصيلات).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Incoming { get; set; }

    /// <summary>صادر: قيمة البضاعة الخارجة من التلاجة.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Outgoing { get; set; }

    public FridgeEntrySource Source { get; set; } = FridgeEntrySource.Manual;

    /// <summary>الحركة الأصلية في مخزون/حساب الاستربس (إن كانت تلقائية).</summary>
    public int? StripInventoryEntryId { get; set; }

    public virtual StripInventoryEntry? StripInventoryEntry { get; set; }

    public int? StripCustomerId { get; set; }

    public virtual StripCustomer? StripCustomer { get; set; }

    /// <summary>حركة الخزنة المرتبطة (للوارد الذي يدخل خزنة أبو مازن).</summary>
    public int? TreasuryEntryId { get; set; }

    public virtual TreasuryEntry? TreasuryEntry { get; set; }
}
