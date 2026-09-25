namespace Axpense.Service.Drivers.Dtos
{
    /// <summary>Create / update payload for a driver.</summary>
    public sealed class DriverUpsertRequest
    {
        public string FullName { get; set; } = "";
        /// <summary>Optional on create — generated (EMP-1001, EMP-1002…) when blank.</summary>
        public string? EmployeeNumber { get; set; }
        public string Status { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? NationalId { get; set; }
        public DateTime? HireDate { get; set; }
        public string LicenseNumber { get; set; } = "";
        public string LicenseClass { get; set; } = "";
        public DateTime? LicenseIssuedDate { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }
        public decimal? Rating { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class DriverListQuery
    {
        /// <summary>Free text matched against name, licence number and employee number.</summary>
        public string? Q { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed record VehicleRefDto(Guid Id, string Name, string PlateNumber);

    public sealed record DriverListItemDto(
        Guid Id,
        string FullName,
        string EmployeeNumber,
        string LicenseClass,
        string? LicenseNumber,
        DateTime? LicenseExpiryDate,
        int? LicenseDaysLeft,
        VehicleRefDto? AssignedVehicle,
        int DocumentCount,
        int ExpiringDocumentCount,
        decimal Rating,
        string Status);

    public sealed record DriverListResponse(
        IReadOnlyList<DriverListItemDto> Items,
        int FilteredCount,
        int TotalCount,
        int Page,
        int PageSize);

    public sealed record DriverDocumentDto(
        Guid Id,
        string Name,
        DateTime ExpiryDate,
        int DaysLeft,
        // Valid | Expiring | Expired
        string State,
        bool HasFile,
        string? FileName,
        string? ContentType);

    public sealed record DriverStatsDto(
        decimal Rating,
        int InspectionsRun,
        int FailedItemsFound,
        int IssuesReported,
        int IssuesOpen);

    /// <summary>One entry of the driver's performance history. Kind: assignment | fuel.</summary>
    public sealed record DriverTimelineEventDto(
        DateTime Date,
        string Kind,
        bool IsCurrent,
        string? VehicleName,
        string? Note,
        DateTime? EndDate,
        string? Station,
        decimal? Liters,
        decimal? Amount);

    public sealed record DriverProfileDto(
        Guid Id,
        string FullName,
        string EmployeeNumber,
        string Status,
        string Phone,
        string? Email,
        string? NationalId,
        DateTime? HireDate,
        string? LicenseNumber,
        string LicenseClass,
        DateTime? LicenseIssuedDate,
        DateTime? LicenseExpiryDate,
        int? LicenseDaysLeft,
        decimal Rating,
        string? Notes,
        VehicleRefDto? CurrentVehicle,
        DriverStatsDto Stats,
        IReadOnlyList<DriverDocumentDto> Documents,
        IReadOnlyList<DriverTimelineEventDto> Timeline);

    public sealed class DriverDocumentCreateRequest
    {
        public string Name { get; set; } = "";
        public DateTime? ExpiryDate { get; set; }
    }

    /// <summary>An uploaded scan, already read from the HTTP request.</summary>
    public sealed record FileUpload(Stream Content, string FileName, string ContentType, long Length);

    public sealed record FileDownload(Stream Content, string FileName, string ContentType);
}
