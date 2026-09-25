namespace Axpense.Data.Entities.DriverEntities
{
    /// <summary>
    /// A person who operates fleet vehicles. Tenant-owned.
    /// Status and licence class values are defined in <see cref="Constants.DriverConstants"/>.
    /// </summary>
    public class Driver : TenantEntity
    {
        // ---- Details ----
        public string FullName { get; set; } = "";
        public string EmployeeNumber { get; set; } = "";
        public string Status { get; set; } = Constants.DriverConstants.StatusActive;
        public string Phone { get; set; } = "";
        public string? Email { get; set; }
        public string? NationalId { get; set; }
        /// <summary>Date the driver joined the organization ("Joined" in the UI).</summary>
        public DateTime? HireDate { get; set; }

        // ---- Licence ----
        public string? LicenseNumber { get; set; }
        public string LicenseClass { get; set; } = Constants.DriverConstants.LicenseClassLight;
        public DateTime? LicenseIssuedDate { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }

        /// <summary>Performance rating, 0.0 – 5.0.</summary>
        public decimal Rating { get; set; } = 4.0m;

        public string? Notes { get; set; }

        public ICollection<DriverDocument> Documents { get; set; } = [];
    }
}
