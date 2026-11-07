using System.ComponentModel.DataAnnotations;

namespace HagImamAccounts.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "اكتب اسم المستخدم.")]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "اكتب كلمة المرور.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "تذكرني")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
