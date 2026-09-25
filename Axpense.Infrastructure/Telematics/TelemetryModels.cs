namespace Axpense.Infrastructure.Telematics
{
    /// <summary>
    /// What the provider needs to know about a vehicle to report on it. <paramref name="FleetStatus"/>
    /// is only a hint for simulators (e.g. keep a vehicle in maintenance at the workshop);
    /// real device adapters ignore it.
    /// </summary>
    public sealed record TelemetryVehicle(Guid VehicleId, string Category, string FuelType, string? FleetStatus = null);

    public sealed record GeoPoint(double Latitude, double Longitude);

    /// <summary>Motion reported by the device: moving, idle (engine on, stationary) or parked.</summary>
    public static class TelemetryMotion
    {
        public const string Moving = "moving";
        public const string Idle = "idle";
        public const string Parked = "parked";
    }

    public sealed record TelemetryTrip(
        string Origin,
        string Destination,
        double ProgressPercent,
        double RemainingKm,
        DateTime EtaUtc,
        IReadOnlyList<GeoPoint> Path);

    public sealed record VehicleTelemetry(
        Guid VehicleId,
        GeoPoint Position,
        double HeadingDegrees,
        int SpeedKmh,
        bool EngineOn,
        string Motion,
        string Address,
        double DistanceTodayKm,
        double FuelUsedToday,
        int IdleMinutesToday,
        TelemetryTrip? Trip,
        string? LastTrip,
        DateTime RecordedAtUtc);
}
