using System.ComponentModel.DataAnnotations;

namespace Axpense.Service.Auth.Dtos
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "اسم المستخدم أو البريد الإلكتروني مطلوب")]
        public string UserNameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        public string Password { get; set; } = string.Empty;
    }
}
