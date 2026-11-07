using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HagImamAccounts.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الموظف مطلوب.")]
        [StringLength(200)]
        [Display(Name = "اسم الموظف")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "الوظيفة")]
        public string? JobTitle { get; set; }

        [StringLength(20)]
        [Display(Name = "رقم الهاتف")]
        public string? Phone { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "الراتب لا يمكن أن يكون سالبًا.")]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "الراتب الشهري")]
        public decimal MonthlySalary { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاريخ التعيين")]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Display(Name = "يعمل حاليًا")]
        public bool IsActive { get; set; } = true;

        [StringLength(1000)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<EmployeeAdvance> Advances { get; set; } = new List<EmployeeAdvance>();

        public virtual ICollection<EmployeeCustody> Custodies { get; set; } = new List<EmployeeCustody>();

        public virtual ICollection<PayrollEntry> PayrollEntries { get; set; } = new List<PayrollEntry>();
    }
}
