using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    /// <summary>
    /// تسوية يدوية على إجمالي رأس المال، تُسجَّل بالفرق بين القيمة المطلوبة والقيمة المحسوبة،
    /// حتى يظل رأس المال قابلاً للتعديل مع الاحتفاظ بسجل بكل تعديل.
    /// </summary>
    public class CapitalAdjustment
    {
        public int Id { get; set; }

        [Display(Name = "التاريخ")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        /// <summary>الفرق المسجّل: موجب يزيد رأس المال، وسالب يقلله.</summary>
        [Display(Name = "قيمة التسوية")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Display(Name = "البيان")]
        [Required(ErrorMessage = "اكتب بيان التسوية.")]
        [StringLength(500)]
        public string Statement { get; set; } = string.Empty;

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
