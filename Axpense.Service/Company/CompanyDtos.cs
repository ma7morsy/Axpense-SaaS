namespace Axpense.Service.Company
{
    public sealed record ValueLabel(string Value, string Label);

    public sealed record CompanyOptionsDto(
        IReadOnlyList<string> Industries, IReadOnlyList<string> Sizes, IReadOnlyList<ValueLabel> TimeZones,
        IReadOnlyList<ValueLabel> Currencies, IReadOnlyList<string> DateFormats, IReadOnlyList<ValueLabel> DistanceUnits,
        IReadOnlyList<ValueLabel> Months);

    public sealed record CompanyProfileDto(
        string CompanyName, string? LegalName, string? Industry, string? CompanySize, string? CommercialRegistrationNo,
        string? TaxId, int? FoundedYear, bool HasLogo, string? LogoVersion,
        string? Address, string? City, string? Country, string? Phone, string? Email, string? Website,
        string TimeZone, string Currency, int FiscalYearStartMonth, string DateFormat, string DistanceUnit,
        DateTime? UpdatedAt);

    public sealed record CompanyProfileRequest(
        string? CompanyName, string? LegalName, string? Industry, string? CompanySize, string? CommercialRegistrationNo,
        string? TaxId, int? FoundedYear,
        string? Address, string? City, string? Country, string? Phone, string? Email, string? Website,
        string? TimeZone, string? Currency, int? FiscalYearStartMonth, string? DateFormat, string? DistanceUnit);

    public sealed record PlanDto(string Key, string Name, decimal Monthly, decimal Annual, int? Vehicles, int? Seats, int StorageGb,
        bool Popular, IReadOnlyList<string> Features);

    public sealed record UsageDto(int Vehicles, int? VehicleLimit, int Seats, int? SeatLimit, long StorageBytes, long StorageLimitBytes);

    public sealed record SubscriptionDto(
        string PlanKey, string PlanName, string BillingCycle, string Status, bool AutoRenew, decimal Price, string Currency,
        DateTime CustomerSince, DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd, int DaysLeft,
        UsageDto Usage, IReadOnlyList<PlanDto> Plans);

    public sealed record ChangePlanRequest(string? PlanKey, string? BillingCycle);
    public sealed record AutoRenewRequest(bool AutoRenew);

    public sealed record InvoiceDto(Guid Id, string Code, DateTime IssueDate, string Description, decimal Amount, string Currency, string Status,
        DateTime PeriodStart, DateTime PeriodEnd);

    public sealed record BillingDto(string? CardBrand, string? CardLast4, string? CardExpiry, string? CardHolder, string? BillingEmail,
        IReadOnlyList<InvoiceDto> Invoices);

    /// <summary>
    /// What the payment provider's tokenization step returns. The full card number and CVC never reach the API.
    /// </summary>
    public sealed record PaymentMethodRequest(string? Brand, string? Last4, string? Expiry, string? Holder);
    public sealed record BillingEmailRequest(string? Email);

    public sealed record InvoiceDocument(string FileName, string Html);
}
