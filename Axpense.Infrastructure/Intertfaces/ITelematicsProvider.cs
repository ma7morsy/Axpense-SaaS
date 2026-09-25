using Axpense.Infrastructure.Telematics;

namespace Axpense.Infrastructure.Intertfaces
{
    /// <summary>
    /// Source of live vehicle telemetry (position, speed, trip, today's usage).
    /// Business code depends only on this interface; a GPS/telematics vendor (Teltonika, Traccar,
    /// Samsara…) is plugged in by adding an adapter that implements it.
    /// </summary>
    public interface ITelematicsProvider
    {
        /// <summary>True when data is generated rather than read from real devices.</summary>
        bool IsSimulated { get; }

        Task<IReadOnlyList<VehicleTelemetry>> GetLatestAsync(Guid organizationId, IReadOnlyList<TelemetryVehicle> vehicles, CancellationToken cancellationToken = default);
    }
}
