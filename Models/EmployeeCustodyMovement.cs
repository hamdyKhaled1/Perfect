using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class EmployeeCustodyMovement
    {
        public int Id { get; set; }

        public int EmployeeCustodyId { get; set; }

        [ForeignKey(nameof(EmployeeCustodyId))]
        public virtual EmployeeCustody? EmployeeCustody { get; set; }

        [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "قيمة الحركة يجب أن تكون أكبر من صفر.")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "اكتب بيان الحركة.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;
    }
}
