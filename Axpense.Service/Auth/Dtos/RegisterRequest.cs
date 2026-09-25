using System.ComponentModel.DataAnnotations;

namespace Axpense.Service.Auth.Dtos
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "اسم المنظمة مطلوب")]
        [StringLength(100)]
        public string OrganizationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم الأول مطلوب")]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم العائلة مطلوب")]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; } = string.Empty;

        /// <summary>Optional: when blank the username is derived from the email address.</summary>
        [StringLength(50)]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور لازم تكون 6 أحرف على الأقل")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare(nameof(Password), ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        // اختياري - لو متبعتش هيتحط "Owner" افتراضيًا (أول يوزر في الـ Organization)
        public string? Role { get; set; }
    }
}
