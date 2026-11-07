using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models;

public class CapitalSetting
{
    public int Id { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ManualCapital { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ClientBalances { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OutsideCustomerBalances { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TreasuryBalance { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal NetExpensesWithoutTreasury { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal FarmAccount { get; set; }
}
