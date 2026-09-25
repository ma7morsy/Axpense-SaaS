namespace Axpense.Service.Auth.Dtos
{
    public class UserSummaryResponse
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsRootSuperAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
