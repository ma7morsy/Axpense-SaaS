using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Infrastructure.Telematics;
using Axpense.Service.Common;
using Axpense.Service.Fleet;
using Axpense.Service.Settings;
using Axpense.Service.Tracking.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Tracking
{
    /// <summary>
    /// Aerial view: merges live telemetry with fleet records (status, driver, PM runway and part wear).
    /// Business overrides telemetry where it knows better: a vehicle in maintenance is "workshop".
    /// Retired vehicles are not tracked.
    /// </summary>
    public sealed class FleetTrackingService : IFleetTrackingService
    {
        public const string MotionWorkshop = "workshop";
        private const int MaxMaintenanceItems = 6;

        private readonly AxpenseDbContext _db;
        private readonly ITelematicsProvider _telematics;

        public FleetTrackingService(AxpenseDbContext db, ITelematicsProvider telematics)
        {
            _db = db;
            _telematics = telematics;
        }

        public async Task<FleetTrackingDto> GetFleetAsync(Guid organizationId, CancellationToken ct = default)
        {
            var vehicles = await TrackedVehicles(organizationId).OrderBy(v => v.Make).ThenBy(v => v.Model).ToListAsync(ct);
            var telemetry = await TelemetryAsync(organizationId, vehicles, ct);
            var drivers = await FleetQueries.CurrentDriversAsync(_db, organizationId, vehicles.Select(v => v.Id).ToList(), ct);

            var items = vehicles
                .Where(v => telemetry.ContainsKey(v.Id))
                .Select(v =>
                {
                    var t = telemetry[v.Id];
                    var motion = MotionOf(v, t);
                    var driver = drivers.GetValueOrDefault(v.Id);
                    return new TrackedVehicleDto(v.Id, Name(v), v.PlateNumber, v.Category, v.FuelType, motion,
                        motion == TelemetryMotion.Moving ? t.SpeedKmh : 0,
                        t.Position.Latitude, t.Position.Longitude, t.HeadingDegrees,
                        driver?.DriverId, driver?.FullName);
                })
                .ToList();

            var counts = new TrackingCountsDto(
                items.Count,
                items.Count(i => i.Motion == TelemetryMotion.Moving),
                items.Count(i => i.Motion == TelemetryMotion.Idle),
                items.Count(i => i.Motion == TelemetryMotion.Parked),
                items.Count(i => i.Motion == MotionWorkshop));

            return new FleetTrackingDto(items, counts, _telematics.IsSimulated, DateTime.UtcNow);
        }

        public async Task<ServiceResult<TrackedVehicleDetailDto>> GetVehicleAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            var v = await TrackedVehicles(organizationId).FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
            if (v is null) return ServiceResult<TrackedVehicleDetailDto>.NotFound("Vehicle not found.");

            var telemetry = await TelemetryAsync(organizationId, [v], ct);
            if (!telemetry.TryGetValue(v.Id, out var t))
                return ServiceResult<TrackedVehicleDetailDto>.NotFound("No telemetry is available for this vehicle.");

            var motion = MotionOf(v, t);
            var inWorkshop = motion == MotionWorkshop;
            var driver = (await FleetQueries.CurrentDriversAsync(_db, organizationId, [v.Id], ct)).GetValueOrDefault(v.Id);

            var today = DateTime.UtcNow.Date;
            var rules = await PmRules.LoadAsync(_db, organizationId, ct);
            var maintenance = new List<MaintenanceDueDto>();
            var pm = PmCalculator.Evaluate(v, today, rules);
            if (pm.Status != "none")
                maintenance.Add(new MaintenanceDueDto(v.Id, "Preventive maintenance", pm.Percent, pm.Status, pm.Label));
            var parts = await _db.VehicleParts.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == (int)CurrentStatusType.Active
                            && x.Status != VehicleConstants.PartRetired)
                .Select(x => new { Part = x, x.PresetPart.Name })
                .ToListAsync(ct);
            maintenance.AddRange(parts
                .Select(x => (x.Part, x.Name, Wear: PartWearCalculator.Evaluate(x.Part, v.CurrentOdometer, v.ReadingUnit, today)))
                .Where(x => x.Wear.Status is "ok" or "due" or "expired")
                .OrderByDescending(x => x.Wear.Percent)
                .Take(MaxMaintenanceItems - maintenance.Count)
                .Select(x => new MaintenanceDueDto(x.Part.Id, x.Name, x.Wear.Percent, x.Wear.Status == "expired" ? "overdue" : x.Wear.Status, x.Wear.Label)));

            var trip = !inWorkshop && t.Trip is not null
                ? new TrackingTripDto(t.Trip.Origin, t.Trip.Destination, t.Trip.ProgressPercent, t.Trip.RemainingKm, t.Trip.EtaUtc,
                    t.Trip.Path.Select(p => new[] { p.Latitude, p.Longitude }).ToList())
                : null;

            return ServiceResult<TrackedVehicleDetailDto>.Ok(new TrackedVehicleDetailDto(
                v.Id, Name(v), v.PlateNumber, v.Category, v.FuelType, motion,
                motion == TelemetryMotion.Moving ? t.SpeedKmh : 0,
                VehicleConstants.SpeedLimitFor(v.Category),
                !inWorkshop && t.EngineOn,
                inWorkshop ? 0 : t.DistanceTodayKm,
                inWorkshop ? 0 : t.FuelUsedToday,
                v.FuelType == VehicleConstants.FuelElectric ? "kWh" : "L",
                inWorkshop ? 0 : t.IdleMinutesToday,
                v.CurrentOdometer,
                t.Address,
                t.Position.Latitude, t.Position.Longitude,
                t.RecordedAtUtc,
                trip,
                inWorkshop ? "Checked in for maintenance" : t.LastTrip,
                driver is null ? null : new TrackingDriverDto(driver.DriverId, driver.FullName, driver.LicenseClass, driver.Rating, driver.Phone),
                maintenance,
                _telematics.IsSimulated));
        }

        private IQueryable<Vehicle> TrackedVehicles(Guid organizationId) =>
            _db.Vehicles.AsNoTracking().Where(v =>
                v.OrganizationId == organizationId &&
                v.CurrentState == (int)CurrentStatusType.Active &&
                v.Status != VehicleConstants.StatusRetired);

        private async Task<Dictionary<Guid, VehicleTelemetry>> TelemetryAsync(Guid organizationId, IReadOnlyList<Vehicle> vehicles, CancellationToken ct)
        {
            var request = vehicles.Select(v => new TelemetryVehicle(v.Id, v.Category, v.FuelType, v.Status)).ToList();
            var data = await _telematics.GetLatestAsync(organizationId, request, ct);
            return data.ToDictionary(t => t.VehicleId);
        }

        private static string MotionOf(Vehicle v, VehicleTelemetry t) =>
            v.Status == VehicleConstants.StatusUnderMaintenance ? MotionWorkshop : t.Motion;

        private static string Name(Vehicle v) => $"{v.Make} {v.Model}".Trim();
    }
}
