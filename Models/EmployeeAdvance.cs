using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class EmployeeAdvance
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }

        [Range(typeof(decimal), "0.0001", "9999999999999999", ErrorMessage = "قيمة السلفة يجب أن تكون أكبر من صفر.")]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "قيمة السلفة")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "التاريخ")]
        public DateTime Date { get; set; } = DateTime.Today;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        /// <summary>
        /// الشهر الذي خُصمت فيه السلفة، ويظل فارغًا حتى يتم ربطها بمرتب شهر.
        /// </summary>
        public int? PayrollEntryId { get; set; }

        [ForeignKey(nameof(PayrollEntryId))]
        public virtual PayrollEntry? PayrollEntry { get; set; }

        [NotMapped]
        public bool IsDeducted => PayrollEntryId.HasValue;
    }
}
