namespace HagImamAccounts.ViewModels;

public class CapitalViewModel
{
    public decimal ClientBalances { get; set; }
    public decimal OutsideCustomerBalances { get; set; }
    public decimal TreasuryBalance { get; set; }
    public decimal NetExpenses { get; set; }
    public decimal FarmAccount { get; set; }

    public decimal TotalCapital =>
        ClientBalances + OutsideCustomerBalances + TreasuryBalance - NetExpenses - FarmAccount;
}
