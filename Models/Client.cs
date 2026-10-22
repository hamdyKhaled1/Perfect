using System.ComponentModel.DataAnnotations;

namespace HagImamAccounts.Models
{
    public class Client
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [Display(Name = "اسم العميل")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "رقم التليفون")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        // Computed property - total due across all transactions
        [Display(Name = "إجمالي ما عليه")]
        public decimal TotalDue => Transactions
            .Where(t => !t.IsDeleted)
            .Sum(t => t.TotalDue);
    }
}
