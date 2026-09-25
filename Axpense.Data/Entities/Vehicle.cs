namespace Axpense.Data.Entities
{
    /// <summary>
    /// A fleet asset. Allowed values for the string fields live in <see cref="Constants.VehicleConstants"/>.
    /// </summary>
    public class Vehicle : TenantEntity
    {
        // ---- Identity
        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public int ModelYear { get; set; }
        public string Category { get; set; } = Constants.VehicleConstants.CategoryTruck;
        public string OwnerType { get; set; } = Constants.VehicleConstants.OwnerCompany;
        public string? OwnerName { get; set; }
        public string? Vin { get; set; }
        public string PlateNumber { get; set; } = "";
        public string Status { get; set; } = Constants.VehicleConstants.StatusActive;

        // ---- Usage & power
        /// <summary>Current reading in <see cref="ReadingUnit"/> (km, or engine hours for machinery).</summary>
        public decimal? CurrentOdometer { get; set; }
        public string ReadingUnit { get; set; } = Constants.VehicleConstants.UnitKm;
        public string EngineType { get; set; } = Constants.VehicleConstants.EngineMechanic;
        public string FuelType { get; set; } = Constants.VehicleConstants.FuelDiesel;
        /// <summary>Litres, or kWh for electric.</summary>
        public decimal? TankCapacity { get; set; }

        // ---- Preventive maintenance criteria
        public string PmTrigger { get; set; } = Constants.VehicleConstants.PmUsage;
        public int? PmIntervalKm { get; set; }
        public int? PmIntervalDays { get; set; }
        public int? PmIntervalHours { get; set; }
        public decimal? PmLastServiceReading { get; set; }
        public DateTime? PmLastServiceDate { get; set; }

        // ---- Purchase
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? Supplier { get; set; }
        public DateTime? WarrantyUntil { get; set; }
        public decimal? ExpectedResidualValue { get; set; }

        // ---- Insurance
        public string? InsuranceProvider { get; set; }
        public string? InsurancePolicyNumber { get; set; }
        public DateTime? InsuranceRenewalDate { get; set; }
        public decimal? InsuranceAnnualPremium { get; set; }

        // ---- Licence & registration
        public string? RegistrationAuthority { get; set; }
        public string? RegistrationNumber { get; set; }
        public DateTime? RegistrationRenewalDate { get; set; }

        // ---- Photo (stored in file storage)
        public string? PhotoKey { get; set; }
        public string? PhotoContentType { get; set; }

        public string? Notes { get; set; }

        public Organization Organization { get; set; } = null!;
    }
}
