using Axpense.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace Axpense.Data.UserApplication
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public Guid OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;

        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? NationalId { get; set; }
        public bool IsRootSuperAdmin { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تعطيل الحساب بدل حذفه
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
