using System.ComponentModel.DataAnnotations;

namespace HagImamAccounts.ViewModels
{
    public class ClientListViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public decimal TotalDue { get; set; }
        public int TransactionCount { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public bool IsFridge { get; set; }
    }

    public class ClientProfileViewModel
    {
        public Models.Client Client { get; set; } = null!;
        public List<Models.Transaction> Transactions { get; set; } = new();
        public List<ClientMovementViewModel> Movements { get; set; } = new();
        public decimal OverallTotalDue { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class ClientMovementViewModel
    {
        public int TransactionId { get; set; }
        public DateTime Date { get; set; }
        public string Statement { get; set; } = string.Empty;
        public decimal NetWeight { get; set; }
        public decimal PricePerKilo { get; set; }
        public decimal MealTotal { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal RemainingBalance { get; set; }
    }

    public class ClientFilterViewModel
    {
        public string? SearchTerm { get; set; }
        public string? SortBy { get; set; } = "Name";
        public List<ClientListViewModel> Clients { get; set; } = new();
        public List<ClientCollectionWarningViewModel> CollectionWarnings { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public decimal TotalDue { get; set; }
        public int TotalTransactions { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class ClientCollectionWarningViewModel
    {
        public int ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal TotalDue { get; set; }
        public DateTime? LastCollectionDate { get; set; }
        public int? DaysSinceCollection { get; set; }
    }

    public class BulkMealViewModel
    {
        public DateTime Date { get; set; } = DateTime.Today;
        public decimal FarmPrice { get; set; }
        [StringLength(100)]
        public string? MealType { get; set; }
        [StringLength(200)]
        public string FarmName { get; set; } = string.Empty;
        public decimal FarmWeight { get; set; }
        [Range(typeof(decimal), "0", "99999999999999.99", ErrorMessage = "قيمة الإضافة لا يمكن أن تكون سالبة.")]
        public decimal FarmAdditionAmount { get; set; }
        [StringLength(500)]
        public string? FarmAdditionStatement { get; set; }
        [Range(typeof(decimal), "0", "99999999999999.99", ErrorMessage = "قيمة الخصم لا يمكن أن تكون سالبة.")]
        public decimal FarmDiscountAmount { get; set; }
        [StringLength(500)]
        public string? FarmDiscountStatement { get; set; }
        public List<BulkMealRowViewModel> Rows { get; set; } = new();
    }

    public class BulkMealRowViewModel
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public decimal OldBalance { get; set; }
        public string? GrossWeightExpression { get; set; }
        public string? EmptyWeightExpression { get; set; }
        public string? PanehGrossWeightExpression { get; set; }
        public string? PanehEmptyWeightExpression { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
        public decimal PriceIncrement { get; set; }
        public decimal PanehPriceIncrement { get; set; }
    }

    public class FarmMealArchiveViewModel
    {
        public List<Models.FarmMealBatch> Batches { get; set; } = new();
        public string? FarmName { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class FarmAccountSummaryViewModel
    {
        public List<FarmAccountSummaryItemViewModel> Farms { get; set; } = new();
        public decimal TotalFarmAccount { get; set; }
        public decimal TotalAddition { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalRemainingOwed { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalMealSales { get; set; }
        public decimal TotalProfit { get; set; }
    }

    public class FarmAccountSummaryItemViewModel
    {
        public string FarmName { get; set; } = string.Empty;
        public int BatchCount { get; set; }
        public decimal FarmWeight { get; set; }
        public decimal FarmAccount { get; set; }
        public decimal Addition { get; set; }
        public decimal Discount { get; set; }
        public decimal Paid { get; set; }
        public decimal RemainingOwed { get; set; }
        public decimal Credit { get; set; }
        public decimal MealSales { get; set; }
        public decimal Profit { get; set; }
    }

    public class FarmAccountLedgerViewModel
    {
        public string FarmName { get; set; } = string.Empty;
        public decimal TotalFarmAccount { get; set; }
        public decimal TotalAddition { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal LegacyPaid { get; set; }
        public decimal RecordedPaid { get; set; }
        public decimal IndependentAddition { get; set; }
        public decimal IndependentDiscount { get; set; }
        public decimal Remaining => TotalFarmAccount + TotalAddition + IndependentAddition
            - TotalDiscount - IndependentDiscount - LegacyPaid - RecordedPaid;
        public decimal TotalCustomerCollections { get; set; }
        public List<FarmAccountLedgerRowViewModel> Rows { get; set; } = new();
        public FarmPaymentInputViewModel NewPayment { get; set; } = new();
        public FarmAccountAdjustmentInputViewModel NewAdjustment { get; set; } = new();
    }

    public class FarmAccountLedgerRowViewModel
    {
        public int Id { get; set; }
        public DateTime? Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Statement { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
        public List<FarmCustomerMealViewModel> CustomerMeals { get; set; } = new();
        public List<FarmCustomerCollectionViewModel> CustomerCollections { get; set; } = new();
    }

    public class FarmCustomerMealViewModel
    {
        public DateTime Date { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public decimal NetWeight { get; set; }
        public decimal MealTotal { get; set; }
    }

    public class FarmCustomerCollectionViewModel
    {
        public DateTime Date { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public decimal Cash { get; set; }
        public decimal Electronic { get; set; }
    }

    public class FarmPaymentInputViewModel
    {
        private static readonly Models.TreasuryAccount[] PaymentAccounts =
        {
            Models.TreasuryAccount.General,
            Models.TreasuryAccount.HajjImamCash,
            Models.TreasuryAccount.AbuMazenCash,
            Models.TreasuryAccount.HajjImamVodafoneCash,
            Models.TreasuryAccount.HajjImamInstaPay,
            Models.TreasuryAccount.AbuMazenVodafoneCash,
            Models.TreasuryAccount.AbuMazenInstaPay
        };

        [Required(ErrorMessage = "اسم المزرعة مطلوب.")]
        [StringLength(200)]
        public string FarmName { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "99999999999999.99", ErrorMessage = "أدخل مبلغًا أكبر من صفر.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "اكتب بيان الدفعة.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        public Models.TreasuryAccount TreasuryAccount { get; set; } = Models.TreasuryAccount.General;

        public static IReadOnlyList<Models.TreasuryAccount> AvailableTreasuryAccounts => PaymentAccounts;

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class FarmPaymentEditViewModel : FarmPaymentInputViewModel
    {
        public int Id { get; set; }
        public bool HasTreasuryEntry { get; set; }
    }

    public class FarmAccountAdjustmentInputViewModel
    {
        [Required]
        [StringLength(200)]
        public string FarmName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اختر إضافة أو خصم.")]
        public Models.FarmAccountAdjustmentType Type { get; set; }

        [Range(typeof(decimal), "0.01", "99999999999999.99", ErrorMessage = "أدخل مبلغًا أكبر من صفر.")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "اكتب بيان الحركة.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;
    }

    public class FarmMealEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "تاريخ الوجبات مطلوب.")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "اسم المزرعة مطلوب.")]
        [StringLength(200)]
        public string FarmName { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "سعر المزرعة لا يمكن أن يكون سالبًا.")]
        public decimal FarmPrice { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "وزن المزرعة لا يمكن أن يكون سالبًا.")]
        public decimal FarmWeight { get; set; }

        public int ReturnPage { get; set; } = 1;
        public int ReturnPageSize { get; set; } = 10;
        public string? ReturnFarmName { get; set; }
    }

    public class AddClientToFarmMealViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "اختر العميل.")]
        public int ClientId { get; set; }

        [StringLength(100)]
        public string? GrossWeight { get; set; }

        [StringLength(100)]
        public string? EmptyWeight { get; set; }

        [StringLength(100)]
        public string? PanehGrossWeight { get; set; }

        [StringLength(100)]
        public string? PanehEmptyWeight { get; set; }

        [StringLength(100)]
        public string? MealType { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class InvoiceViewModel
    {
        public Models.Transaction Transaction { get; set; } = null!;
        public List<Models.Transaction> MealTransactions { get; set; } = new();
        public decimal MealTotal => MealTransactions.Sum(meal => meal.MealTotal);
        public decimal OpeningBalance => Transaction.OldBalance;
        public decimal CashCollectionTotal => Transaction.CashCollection;
        public decimal ElectronicCollectionTotal => Transaction.ElectronicCollection;
        public decimal AdditionTotal => Transaction.AdditionAmount;
        public decimal DiscountTotal => Transaction.DiscountAmount;
        public decimal TotalCollected => CashCollectionTotal + ElectronicCollectionTotal;
        public decimal EndingBalance =>
            OpeningBalance + MealTotal + AdditionTotal - DiscountTotal - TotalCollected;
        public string? AdditionNotes => Transaction.AdditionNotes;
        public string? DiscountNotes => Transaction.DiscountNotes;
    }

    public class OutsidePageViewModel
    {
        public List<Models.OutsideCustomer> Customers { get; set; } = new();
        public int? SelectedCustomerId { get; set; }
        public string? SelectedCustomerName { get; set; }
        public decimal RemainingBalance { get; set; }
        public List<OutsideEntryViewModel> Entries { get; set; } = new();
        public OutsideCustomerInputViewModel NewCustomer { get; set; } = new();
        public OutsideEntryInputViewModel NewEntry { get; set; } = new();
    }

    public class OutsideCustomersViewModel
    {
        public string? SearchTerm { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public List<OutsideCustomerListItemViewModel> Customers { get; set; } = new();
        public List<OutsideCustomerCollectionWarningViewModel> CollectionWarnings { get; set; } = new();
    }

    public class OutsideCustomerCollectionWarningViewModel
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? LastCollectionDate { get; set; }
        public int? DaysSinceCollection { get; set; }
    }

    public class OutsideCustomerListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int EntryCount { get; set; }
        public DateTime? LastEntryDate { get; set; }
        public decimal Balance { get; set; }
    }

    public class OutsideCustomerInputViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }
    }

    public class OutsideEntryInputViewModel
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "الوارد لا يمكن أن يكون سالبًا.")]
        public decimal Incoming { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "المبلغ الذي أخذه لا يمكن أن يكون سالبًا.")]
        public decimal Taken { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class OutsideEntryViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Statement { get; set; } = string.Empty;
        public decimal Incoming { get; set; }
        public decimal Taken { get; set; }
        public decimal Balance { get; set; }
    }

    public class StripInventoryPageViewModel
    {
        public Models.StripCartonType SelectedCartonType { get; set; }
        public int FiveKilogramBalance { get; set; }
        public int NineHundredGramBalance { get; set; }
        public List<StripInventoryEntryViewModel> Entries { get; set; } = new();
        public StripInventoryEntryInputViewModel NewEntry { get; set; } = new();
    }

    public class StripCustomersViewModel
    {
        public string? SearchTerm { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public decimal TotalSales { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalDue => TotalSales - TotalPaid;
        public List<StripCustomerListItemViewModel> Customers { get; set; } = new();
    }

    public class StripCustomerListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int FiveKilogramBalance { get; set; }
        public int NineHundredGramBalance { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public int EntryCount { get; set; }
        public DateTime? LastEntryDate { get; set; }
    }

    public class StripCustomerInputViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اكتب اسم العميل.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }
    }

    public class StripCustomerProfileViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int FiveKilogramBalance { get; set; }
        public int NineHundredGramBalance { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalDue => TotalSales - TotalPaid;
        public List<StripCustomerEntryViewModel> Entries { get; set; } = new();
        public StripCustomerEntryInputViewModel NewEntry { get; set; } = new();
        public StripCustomerPaymentInputViewModel NewPayment { get; set; } = new();
    }

    public class StripCustomerEntryViewModel
    {
        public int Id { get; set; }
        public Models.StripCartonType CartonType { get; set; }
        public Models.StripEntryType EntryType { get; set; }
        public DateTime Date { get; set; }
        public string Statement { get; set; } = string.Empty;
        public int Cartons { get; set; }
        public decimal PricePerCarton { get; set; }
        public decimal Amount { get; set; }
        public decimal PaidAmount { get; set; }
        public int Incoming { get; set; }
        public int Outgoing { get; set; }
        public int Balance { get; set; }
        public decimal RunningDue { get; set; }
    }

    public class StripCustomerEntryInputViewModel
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Models.StripCartonType CartonType { get; set; }
        public Models.StripEntryType EntryType { get; set; } = Models.StripEntryType.Sale;

        [StringLength(500)]
        public string? Statement { get; set; }

        [Range(0, 1000000000, ErrorMessage = "عدد الكراتين غير صحيح.")]
        public int Cartons { get; set; }

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "سعر الكرتونة غير صحيح.")]
        public decimal PricePerCarton { get; set; }

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "المبلغ المدفوع غير صحيح.")]
        public decimal PaidAmount { get; set; }

        public Models.TreasuryAccount TreasuryAccount { get; set; } = Models.TreasuryAccount.AbuMazenCash;

        public int Incoming { get; set; }

        public int Outgoing { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class StripCustomerPaymentInputViewModel
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = "دفعة";

        [Range(typeof(decimal), "0.01", "99999999999999.9999", ErrorMessage = "أدخل مبلغ الدفع أكبر من صفر.")]
        public decimal PaidAmount { get; set; }

        public Models.TreasuryAccount TreasuryAccount { get; set; } = Models.TreasuryAccount.AbuMazenCash;

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class FridgePageViewModel
    {
        public int ClientId { get; set; }
        public decimal TotalOutgoing { get; set; }
        public decimal TotalIncoming { get; set; }
        public decimal Balance => TotalOutgoing - TotalIncoming;
        public int StripCustomerCount { get; set; }
        public decimal StripPaidTotal { get; set; }
        public List<FridgeEntryRowViewModel> Entries { get; set; } = new();
        public FridgeEntryInputViewModel NewEntry { get; set; } = new();
    }

    public class FridgeEntryRowViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Statement { get; set; } = string.Empty;
        public decimal Incoming { get; set; }
        public decimal Outgoing { get; set; }
        public decimal Balance { get; set; }
        public Models.FridgeEntrySource Source { get; set; }
        public int? StripCustomerId { get; set; }
        public string? StripCustomerName { get; set; }
        public bool HasTreasuryEntry { get; set; }
        public string? TreasuryAccountName { get; set; }
        public bool IsManual => Source == Models.FridgeEntrySource.Manual;
    }

    public class FridgeEntryInputViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الوارد غير صحيحة.")]
        public decimal? Incoming { get; set; }

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الصادر غير صحيحة.")]
        public decimal? Outgoing { get; set; }

        public Models.TreasuryAccount TreasuryAccount { get; set; } = Models.TreasuryAccount.AbuMazenCash;

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class StripSalesReportViewModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? CustomerId { get; set; }
        public List<StripSalesRowViewModel> Rows { get; set; } = new();
        public List<StripSalesCustomerSummaryViewModel> Summaries { get; set; } = new();
        public int TotalCartons => Rows.Sum(row => row.Cartons);
        public decimal TotalSales => Rows.Sum(row => row.Amount);
        public decimal TotalPaid => Summaries.Sum(summary => summary.TotalPaid);
        public decimal TotalDue => TotalSales - TotalPaid;
    }

    public class StripSalesCustomerSummaryViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int FiveKilogramCartons { get; set; }
        public int NineHundredGramCartons { get; set; }
        public int TotalCartons => FiveKilogramCartons + NineHundredGramCartons;
        public decimal TotalSales { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Remaining => TotalSales - TotalPaid;
    }

    public class StripSalesRowViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public Models.StripCartonType CartonType { get; set; }
        public string Statement { get; set; } = string.Empty;
        public int Cartons { get; set; }
        public decimal PricePerCarton { get; set; }
        public decimal Amount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Remaining => Amount - PaidAmount;
    }

    public class StripInventoryOverviewViewModel
    {
        public int FiveKilogramBalance { get; set; }
        public int NineHundredGramBalance { get; set; }
    }

    public class StripInventoryEntryViewModel
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime Date { get; set; }
        public string ReceivedBy { get; set; } = string.Empty;
        public string Statement { get; set; } = string.Empty;
        public int Incoming { get; set; }
        public int Outgoing { get; set; }
        public int Balance { get; set; }
    }

    public class StripInventoryEntryInputViewModel
    {
        public int Id { get; set; }

        public Models.StripCartonType CartonType { get; set; }

        [Required(ErrorMessage = "اكتب اسم الشخص الذي استلم.")]
        [StringLength(200)]
        public string ReceivedBy { get; set; } = string.Empty;

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Range(0, 1000000000, ErrorMessage = "عدد الكراتين الواردة غير صحيح.")]
        public int Incoming { get; set; }

        [Range(0, 1000000000, ErrorMessage = "عدد الكراتين المسحوبة غير صحيح.")]
        public int Outgoing { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class TreasuryAccountSummaryViewModel
    {
        public Models.TreasuryAccount Account { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public int EntryCount { get; set; }
    }

    public class TreasuryMovementViewModel
    {
        public Models.TreasuryAccount Account { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public List<TreasuryMovementRowViewModel> Entries { get; set; } = new();
        public TreasuryEntryInputViewModel NewEntry { get; set; } = new();
        public TreasuryTransferInputViewModel NewTransfer { get; set; } = new();
        public TreasuryWalletMovementInputViewModel NewWalletMovement { get; set; } = new();
    }

    public class TreasuryMovementRowViewModel
    {
        public int Id { get; set; }
        public Models.TreasuryAccount Account { get; set; }
        public DateTime Date { get; set; }
        public string Statement { get; set; } = string.Empty;
        public decimal Incoming { get; set; }
        public decimal Outgoing { get; set; }
        public decimal Balance { get; set; }
        public bool IsTransfer { get; set; }
        public bool IsFridgeLinked { get; set; }
        public bool IsExpenseLinked { get; set; }
        public int? TransactionId { get; set; }
    }

    public class TreasuryEntryInputViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الوارد غير صحيحة.")]
        public decimal? Incoming { get; set; }

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الصادر غير صحيحة.")]
        public decimal? Outgoing { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class TreasuryTransferInputViewModel
    {
        [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "أدخل مبلغ تحويل أكبر من صفر.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "اكتب بيان التحويل.")]
        [StringLength(400)]
        public string Statement { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Today;
    }

    public class TreasuryWalletMovementInputViewModel
    {
        [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "أدخل مبلغًا أكبر من صفر.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(400)]
        public string Statement { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Today;

        public bool IsFreeze { get; set; }
    }

    public class ExpenseLedgerViewModel
    {
        public List<Models.ExpenseEntry> Entries { get; set; } = new();
        public ExpenseEntryInputViewModel NewEntry { get; set; } = new();
        public decimal TotalIncoming { get; set; }
        public decimal TotalOutgoing { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public Dictionary<int, decimal> RemainingAfterEntry { get; set; } = new();
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public decimal Balance => TotalIncoming - TotalOutgoing;
    }

    public class ExpenseEntryInputViewModel
    {
        /// <summary>الخزن والمحافظ المتاحة للصرف منها عند تسجيل حركة صادر.</summary>
        public static IReadOnlyList<Models.TreasuryAccount> OutgoingTreasuryAccounts { get; } = new[]
        {
            Models.TreasuryAccount.AbuMazenCash,
            Models.TreasuryAccount.AbuMazenVodafoneCash,
            Models.TreasuryAccount.AbuMazenInstaPay,
            Models.TreasuryAccount.MohamedAbdelrahmanCash,
            Models.TreasuryAccount.MohamedAbdelrahmanVodafoneCash,
            Models.TreasuryAccount.MohamedAbdelrahmanInstaPay
        };

        public int Id { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الوارد غير صحيحة.")]
        public decimal? Incoming { get; set; }

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الصادر غير صحيحة.")]
        public decimal? Outgoing { get; set; }

        [Display(Name = "الصادر من")]
        public Models.TreasuryAccount? TreasuryAccount { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;

        /// <summary>أرصدة الخزن المتاحة للصرف، تُملأ عند العرض فقط.</summary>
        public Dictionary<Models.TreasuryAccount, decimal> TreasuryBalances { get; set; } = new();
    }
}
