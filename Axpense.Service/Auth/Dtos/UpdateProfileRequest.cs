using System.ComponentModel.DataAnnotations;

namespace Axpense.Service.Auth.Dtos
{
    // بيانات اليوزر اللي مسجل دخول بيعدلها لنفسه بس (مش UserName - بيفضل ثابت زي ما هو)
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "الاسم الأول مطلوب")]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم العائلة مطلوب")]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }
    }
}
