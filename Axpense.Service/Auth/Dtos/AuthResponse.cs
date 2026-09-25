namespace Axpense.Service.Auth.Dtos
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<string>? Errors { get; set; }

        /// <summary>Machine-readable outcome (INVALID_CREDENTIALS, LOCKED_OUT, EMAIL_TAKEN, USERNAME_TAKEN,
        /// PASSWORD_INVALID, INVALID_RESET_TOKEN) so the client can show a translated message.</summary>
        public string? Code { get; set; }

        /// <summary>Field-level errors (email, userName, password) for the register / reset forms.</summary>
        public Dictionary<string, string>? FieldErrors { get; set; }

        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }

        public Guid? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }

        public string? Token { get; set; }
        public DateTime? ExpiresOn { get; set; }

        // بيتملي بس في ForgotPassword مؤقتًا لحد ما نضيف خدمة إيميل حقيقية -
        // في الإنتاج المفروض التوكن ده يترسل بإيميل مش يرجع في الـ Response
        // Returned only in the Development environment (the controller clears it otherwise).
        public string? ResetToken { get; set; }
    }
}
