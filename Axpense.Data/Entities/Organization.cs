using Axpense.Data.UserApplication;

namespace Axpense.Data.Entities
{
    public class Organization : BaseEntity
    {
        public string Name { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public ICollection<ApplicationUser> Users { get; set; } = [];
        public ICollection<Vehicle> Vehicles { get; set; } = [];
    }
}
