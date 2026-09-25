using System.ComponentModel.DataAnnotations;

namespace Axpense.Service.Auth.Dtos
{
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; } = string.Empty;
    }
}
