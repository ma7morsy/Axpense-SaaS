using System.ComponentModel.DataAnnotations;

 namespace Axpense.Service.Auth.Dtos
{
    // تغيير الباسورد وانت عارف الباسورد القديم (مسجل دخول بالفعل)
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور لازم تكون 6 أحرف على الأقل")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور الجديدة مطلوب")]
        [Compare(nameof(NewPassword), ErrorMessage = "كلمة المرور الجديدة وتأكيدها غير متطابقين")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
