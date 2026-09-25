using System.ComponentModel.DataAnnotations;

namespace Axpense.Service.Auth.Dtos
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "التوكن مطلوب")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور لازم تكون 6 أحرف على الأقل")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور الجديدة مطلوب")]
        [Compare(nameof(NewPassword), ErrorMessage = "كلمة المرور الجديدة وتأكيدها غير متطابقين")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
