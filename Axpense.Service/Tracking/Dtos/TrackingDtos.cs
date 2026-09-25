namespace Axpense.Service.Tracking.Dtos
{
    public sealed record TrackingCountsDto(int All, int Moving, int Idle, int Parked, int Workshop);

    public sealed record TrackedVehicleDto(
        Guid Id,
        string Name,
        string PlateNumber,
        string Category,
        string FuelType,
        // moving | idle | parked | workshop
        string Motion,
        int SpeedKmh,
        double Latitude,
        double Longitude,
        double HeadingDegrees,
        Guid? DriverId,
        string? DriverName);

    public sealed record FleetTrackingDto(
        IReadOnlyList<TrackedVehicleDto> Items,
        TrackingCountsDto Counts,
        bool Simulated,
        DateTime UpdatedAtUtc);

    public sealed record TrackingTripDto(
        string Origin,
        string Destination,
        double ProgressPercent,
        double RemainingKm,
        DateTime EtaUtc,
        IReadOnlyList<double[]> Path);

    public sealed record TrackingDriverDto(Guid Id, string FullName, string LicenseClass, decimal Rating, string Phone);

    public sealed record MaintenanceDueDto(Guid Id, string Name, decimal UsedPercent, string Status, string Label);

    public sealed record TrackedVehicleDetailDto(
        Guid Id,
        string Name,
        string PlateNumber,
        string Category,
        string FuelType,
        string Motion,
        int SpeedKmh,
        int SpeedLimitKmh,
        bool EngineOn,
        double DistanceTodayKm,
        double FuelUsedToday,
        // L | kWh
        string FuelUnit,
        int IdleMinutesToday,
        decimal? OdometerKm,
        string Address,
        double Latitude,
        double Longitude,
        DateTime UpdatedAtUtc,
        TrackingTripDto? Trip,
        string? LastTrip,
        TrackingDriverDto? Driver,
        IReadOnlyList<MaintenanceDueDto> MaintenanceDue,
        bool Simulated);
}
