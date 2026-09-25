using Axpense.Service.Fleet;

namespace Axpense.Service.Vehicles.Dtos
{
    // =====================================================================
    // Register (list) and profile
    // =====================================================================

    public sealed record VehicleDriverDto(Guid Id, string FullName, string LicenseClass, string LicenseNumber, decimal Rating, string Phone, DateTime? Since);

    public sealed class VehicleListQuery
    {
        /// <summary>Free text over plate, VIN, make, model and the assigned driver.</summary>
        public string? Q { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Owner { get; set; }
        /// <summary>ok | due | overdue | none</summary>
        public string? Pm { get; set; }
    }

    public sealed record VehicleListItemDto(
        Guid Id,
        string Name,
        string Make,
        string Model,
        int ModelYear,
        string Category,
        string OwnerType,
        string? OwnerName,
        string? Vin,
        string PlateNumber,
        string Status,
        string FuelType,
        decimal? CurrentOdometer,
        string ReadingUnit,
        bool HasPhoto,
        PmState Pm,
        bool DispatchBlocked,
        VehicleDriverDto? CurrentDriver,
        /// <summary>Open renewal / maintenance alerts on this vehicle (warning or critical).</summary>
        int AlertCount);

    public sealed record VehicleListResponse(IReadOnlyList<VehicleListItemDto> Items, int Total);

    /// <summary>A renewal or maintenance notice shown on the profile (and turned into notifications).</summary>
    public sealed record VehicleAlertDto(
        // pm | insurance | registration | part | warranty
        string Kind,
        // critical | warning | info
        string Severity,
        string Title,
        string Detail,
        // schedule | renew | raise | replace | claim
        string? Action,
        DateTime? DueDate,
        /// <summary>Stable key used to de-duplicate notifications.</summary>
        string Key);

    public sealed record VehicleTabCountsDto(int Parts, int Issues, int Fuel, int Odometer, int Maintenance, int Expenses, int Inspections, int Drivers);

    public sealed record VehicleDetailDto(
        Guid Id,
        string Name,
        // Identity
        string Make, string Model, int ModelYear, string Category, string OwnerType, string? OwnerName,
        string? Vin, string PlateNumber, string Status,
        // Usage & power
        decimal? CurrentOdometer, string ReadingUnit, string EngineType, string FuelType, decimal? TankCapacity, string TankUnit,
        // PM criteria
        string PmTrigger, int? PmIntervalKm, int? PmIntervalDays, int? PmIntervalHours, decimal? PmLastServiceReading, DateTime? PmLastServiceDate,
        // Purchase
        DateTime? PurchaseDate, decimal? PurchasePrice, string? Supplier, DateTime? WarrantyUntil, decimal? ExpectedResidualValue,
        // Insurance
        string? InsuranceProvider, string? InsurancePolicyNumber, DateTime? InsuranceRenewalDate, decimal? InsuranceAnnualPremium,
        // Registration
        string? RegistrationAuthority, string? RegistrationNumber, DateTime? RegistrationRenewalDate,
        string? Notes,
        bool HasPhoto,
        // Derived
        PmState Pm,
        bool DispatchBlocked,
        string? EscalatedTo,
        int? InsuranceDaysLeft,
        int? RegistrationDaysLeft,
        int? WarrantyDaysLeft,
        decimal LifetimeSpend,
        VehicleDriverDto? CurrentDriver,
        IReadOnlyList<VehicleAlertDto> Alerts,
        VehicleTabCountsDto Counts);

    public sealed class VehicleUpsertRequest
    {
        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public int? ModelYear { get; set; }
        public string Category { get; set; } = "";
        public string OwnerType { get; set; } = "";
        public string? OwnerName { get; set; }
        public string? Vin { get; set; }
        public string PlateNumber { get; set; } = "";
        public string? Status { get; set; }

        public decimal? CurrentOdometer { get; set; }
        public string? ReadingUnit { get; set; }
        public string EngineType { get; set; } = "";
        public string FuelType { get; set; } = "";
        public decimal? TankCapacity { get; set; }

        public string? PmTrigger { get; set; }
        public int? PmIntervalKm { get; set; }
        public int? PmIntervalDays { get; set; }
        public int? PmIntervalHours { get; set; }
        public decimal? PmLastServiceReading { get; set; }
        public DateTime? PmLastServiceDate { get; set; }

        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? Supplier { get; set; }
        public DateTime? WarrantyUntil { get; set; }
        public decimal? ExpectedResidualValue { get; set; }

        public string? InsuranceProvider { get; set; }
        public string? InsurancePolicyNumber { get; set; }
        public DateTime? InsuranceRenewalDate { get; set; }
        public decimal? InsuranceAnnualPremium { get; set; }

        public string? RegistrationAuthority { get; set; }
        public string? RegistrationNumber { get; set; }
        public DateTime? RegistrationRenewalDate { get; set; }

        public string? Notes { get; set; }
    }

    public sealed class VehicleStatusRequest
    {
        public string Status { get; set; } = "";
    }

    public sealed class VehicleHandOffRequest
    {
        /// <summary>New driver; null only ends the current assignment.</summary>
        public Guid? DriverId { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public string? Note { get; set; }
        /// <summary>Start the built-in "Handoff inspection" right after the hand-off.</summary>
        public bool PerformInspection { get; set; }
        public string? InspectorName { get; set; }
    }

    public sealed record VehicleHandOffResult(VehicleDetailDto Vehicle, Guid? InspectionId, string? InspectionMessage);

    public sealed record VehicleOptionsDto(
        IReadOnlyList<string> Statuses,
        IReadOnlyList<string> Categories,
        IReadOnlyList<string> OwnerTypes,
        IReadOnlyList<string> ReadingUnits,
        IReadOnlyList<string> EngineTypes,
        IReadOnlyList<string> FuelTypes,
        IReadOnlyList<string> PmTriggers,
        IReadOnlyList<string> PartStatuses,
        IReadOnlyList<string> IssuePriorities,
        IReadOnlyList<string> IssueStatuses,
        IReadOnlyList<string> IssueSources,
        IReadOnlyList<string> ReadingSources,
        IReadOnlyList<string> MaintenanceTypes,
        int RenewalReminderDays);

    // =====================================================================
    // Records (profile tabs)
    // =====================================================================

    public sealed record VehiclePartDto(
        Guid Id, string Code, Guid PresetPartId, string PartName, string? PartNameAr, string PartCode,
        Guid CategoryId, string CategoryName,
        string? Serial, decimal? UnitCost, int? LifespanKm, int? LifespanMonths,
        decimal InstalledReading, DateTime InstalledDate, DateTime? WarrantyUntil, int? WarrantyKm,
        string Status, DateTime? RetiredDate, string? Notes, PartWear Wear);

    public sealed class VehiclePartRequest
    {
        public Guid? PresetPartId { get; set; }
        public string? Serial { get; set; }
        public decimal? UnitCost { get; set; }
        public int? LifespanKm { get; set; }
        public int? LifespanMonths { get; set; }
        public decimal? InstalledReading { get; set; }
        public DateTime? InstalledDate { get; set; }
        public DateTime? WarrantyUntil { get; set; }
        public int? WarrantyKm { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class VehiclePartReplaceRequest
    {
        /// <summary>File a warranty claim (only honoured while the old part is covered): the new part costs nothing.</summary>
        public bool ClaimWarranty { get; set; }
        public string? Serial { get; set; }
        public decimal? UnitCost { get; set; }
        public int? LifespanKm { get; set; }
        public int? LifespanMonths { get; set; }
        public decimal? InstalledReading { get; set; }
        public DateTime? InstalledDate { get; set; }
        public DateTime? WarrantyUntil { get; set; }
        public int? WarrantyKm { get; set; }
        public bool RaiseWorkOrder { get; set; }
        public bool RecordExpense { get; set; }
    }

    public sealed record VehiclePartReplaceResult(VehiclePartDto Retired, VehiclePartDto Fitted, bool WarrantyClaimed, Guid? MaintenanceId, Guid? ExpenseId);

    public sealed record VehicleIssueDto(
        Guid Id, string Code, string Title, string? Note, Guid? PresetPartId, string? PartName,
        string Priority, string Source, string Status, DateTime ReportedDate, DateTime? ResolvedDate);

    public sealed class VehicleIssueRequest
    {
        public string Title { get; set; } = "";
        public string? Note { get; set; }
        public Guid? PresetPartId { get; set; }
        public string? Priority { get; set; }
        public string? Source { get; set; }
        public DateTime? ReportedDate { get; set; }
    }

    public sealed class VehicleIssueStatusRequest
    {
        public string Status { get; set; } = "";
    }

    public sealed record FuelRecordDto(
        Guid Id, DateTime Date, string? Station, decimal Quantity, string Unit, decimal UnitPrice, decimal TotalAmount,
        decimal? Odometer, decimal? ConsumptionPer100);

    public sealed class FuelRecordRequest
    {
        public DateTime? Date { get; set; }
        public string? Station { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? TotalCost { get; set; }
        public decimal? Odometer { get; set; }
    }

    public sealed record OdometerReadingDto(Guid Id, DateTime Date, decimal Value, decimal? DistanceSincePrevious, string Source, string? RecordedBy);

    public sealed class OdometerReadingRequest
    {
        public DateTime? Date { get; set; }
        public decimal? Value { get; set; }
        public string? Source { get; set; }
        public string? RecordedBy { get; set; }
    }

    public sealed record VehicleExpenseDto(Guid Id, DateTime Date, string Title, Guid? ExpenseTypeId, string TypeName, string? TypeColor, string? Note, decimal Amount);

    public sealed class VehicleExpenseRequest
    {
        public string Title { get; set; } = "";
        public Guid? ExpenseTypeId { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? Date { get; set; }
        public string? Note { get; set; }
    }

    public sealed record VehicleInspectionDto(Guid Id, string Code, string TemplateName, DateTime Date, decimal? Odometer, string? InspectorName, string Status, int Total, int Passed, int Failed, int NotApplicable);

    public sealed record VehicleAssignmentDto(Guid Id, Guid DriverId, string DriverName, string? LicenseNumber, DateTime From, DateTime? To, string? Note, bool Current);
}
