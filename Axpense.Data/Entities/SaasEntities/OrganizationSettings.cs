namespace Axpense.Data.Entities.SaasEntities
{
    /// <summary>Company profile and regional preferences (Administration → Company → Company info).</summary>
    public class OrganizationSettings : TenantEntity
    {
        // ---- Identity
        public string CompanyName { get; set; } = "";
        public string? LegalName { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? CommercialRegistrationNo { get; set; }
        public string? TaxId { get; set; }
        public int? FoundedYear { get; set; }
        public string? LogoUrl { get; set; }
        /// <summary>Logo in file storage (served through the API).</summary>
        public string? LogoKey { get; set; }
        public string? LogoContentType { get; set; }

        // ---- Contact & address
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }

        // ---- Regional settings
        public string Currency { get; set; } = "EGP";
        public string TimeZone { get; set; } = "Africa/Cairo";
        public string DateFormat { get; set; } = "DD/MM/YYYY";
        /// <summary>1 = January.</summary>
        public int FiscalYearStartMonth { get; set; } = 1;
        public string DistanceUnit { get; set; } = "km";

        /// <summary>Set when the owner finishes or skips the onboarding wizard. Null = the wizard is shown after sign-in.</summary>
        public DateTime? OnboardingCompletedAt { get; set; }

        // ---- Legacy notification switches (kept for compatibility)
        public bool EmailNotifications { get; set; } = true;
        public bool MaintenanceReminders { get; set; } = true;
        public bool LicenseReminders { get; set; } = true;
    }
}
