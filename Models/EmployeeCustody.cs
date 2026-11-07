using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class EmployeeCustody
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }

        [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "قيمة العهدة يجب أن تكون أكبر من صفر.")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "اكتب بيان العهدة.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        public TreasuryAccount TreasuryAccount { get; set; }

        public int? TreasuryEntryId { get; set; }

        [ForeignKey(nameof(TreasuryEntryId))]
        public virtual TreasuryEntry? TreasuryEntry { get; set; }

        public virtual ICollection<EmployeeCustodyMovement> Movements { get; set; } = new List<EmployeeCustodyMovement>();
    }
}
