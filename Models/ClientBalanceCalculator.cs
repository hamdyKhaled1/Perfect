namespace HagImamAccounts.Models
{
    public static class ClientBalanceCalculator
    {
        public static decimal Calculate(IEnumerable<Transaction> transactions)
        {
            var activeTransactions = transactions
                .Where(transaction => !transaction.IsDeleted)
                .OrderBy(transaction => transaction.Id)
                .ToList();

            if (activeTransactions.Count == 0)
                return 0;

            return activeTransactions[0].OldBalance + activeTransactions.Sum(transaction =>
                transaction.MealTotal +
                transaction.AdditionAmount -
                transaction.CashCollection -
                transaction.ElectronicCollection -
                transaction.DiscountAmount);
        }
    }
}
