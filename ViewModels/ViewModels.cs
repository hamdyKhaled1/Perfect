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
    }

    public class ClientProfileViewModel
    {
        public Models.Client Client { get; set; } = null!;
        public List<Models.Transaction> Transactions { get; set; } = new();
        public decimal OverallTotalDue { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class ClientFilterViewModel
    {
        public string? SearchTerm { get; set; }
        public string? SortBy { get; set; } = "Name";
        public List<ClientListViewModel> Clients { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public decimal TotalDue { get; set; }
        public int TotalTransactions { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class BulkMealViewModel
    {
        public DateTime Date { get; set; } = DateTime.Today;
        public string DateText { get; set; } = string.Empty;
        public decimal FarmPrice { get; set; }
        [StringLength(100)]
        public string? MealType { get; set; }
        [StringLength(200)]
        public string FarmName { get; set; } = string.Empty;
        public decimal FarmWeight { get; set; }
        public List<BulkMealRowViewModel> Rows { get; set; } = new();
    }

    public class BulkMealRowViewModel
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public decimal OldBalance { get; set; }
        public string? GrossWeightExpression { get; set; }
        public string? EmptyWeightExpression { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
        public decimal PriceIncrement { get; set; }
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

    public class InvoiceViewModel
    {
        public Models.Transaction Transaction { get; set; } = null!;
        public Models.Transaction AccountingTransaction { get; set; } = null!;
        public Models.Transaction MealTransaction { get; set; } = null!;
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
        public List<StripCustomerListItemViewModel> Customers { get; set; } = new();
    }

    public class StripCustomerListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int FiveKilogramBalance { get; set; }
        public int NineHundredGramBalance { get; set; }
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
        public List<StripCustomerEntryViewModel> Entries { get; set; } = new();
        public StripCustomerEntryInputViewModel NewEntry { get; set; } = new();
    }

    public class StripCustomerEntryViewModel
    {
        public int Id { get; set; }
        public Models.StripCartonType CartonType { get; set; }
        public DateTime Date { get; set; }
        public string Statement { get; set; } = string.Empty;
        public int Incoming { get; set; }
        public int Outgoing { get; set; }
        public int Balance { get; set; }
    }

    public class StripCustomerEntryInputViewModel
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Models.StripCartonType CartonType { get; set; }

        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Range(0, 1000000000, ErrorMessage = "عدد الكراتين الواردة غير صحيح.")]
        public int Incoming { get; set; }

        [Range(0, 1000000000, ErrorMessage = "عدد الكراتين المسحوبة غير صحيح.")]
        public int Outgoing { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
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

    public class ExpenseLedgerViewModel
    {
        public List<Models.ExpenseEntry> Entries { get; set; } = new();
        public ExpenseEntryInputViewModel NewEntry { get; set; } = new();
        public decimal TotalIncoming { get; set; }
        public decimal TotalOutgoing { get; set; }
        public decimal Balance => TotalIncoming - TotalOutgoing;
    }

    public class ExpenseEntryInputViewModel
    {
        [Required(ErrorMessage = "اكتب البيان.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الوارد غير صحيحة.")]
        public decimal? Incoming { get; set; }

        [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "قيمة الصادر غير صحيحة.")]
        public decimal? Outgoing { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
    }
}
