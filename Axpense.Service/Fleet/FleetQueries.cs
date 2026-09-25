using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Fleet
{
    public sealed record CurrentDriver(Guid DriverId, string FullName, string LicenseClass, decimal Rating, string Phone,
        string LicenseNumber = "", Guid AssignmentId = default, DateTime Since = default);

    /// <summary>Read helpers shared by fleet-facing services.</summary>
    public static class FleetQueries
    {
        /// <summary>
        /// Driver currently holding each vehicle: an assignment covering today (start ≤ today and
        /// no end or end after today — an assignment ending today has been handed off); the latest start wins.
        /// </summary>
        public static async Task<Dictionary<Guid, CurrentDriver>> CurrentDriversAsync(
            AxpenseDbContext db, Guid organizationId, IReadOnlyCollection<Guid> vehicleIds, CancellationToken ct)
        {
            if (vehicleIds.Count == 0) return new();
            var today = DateTime.UtcNow.Date;
            var rows = await db.VehicleAssignments.AsNoTracking()
                .Where(a => a.OrganizationId == organizationId && vehicleIds.Contains(a.VehicleId)
                            && a.CurrentState == (int)CurrentStatusType.Active
                            && a.StartDate < today.AddDays(1) && (a.EndDate == null || a.EndDate > today)
                            && a.Driver.CurrentState == (int)CurrentStatusType.Active)
                .Select(a => new
                {
                    a.Id, a.VehicleId, a.StartDate, a.DriverId,
                    a.Driver.FullName, a.Driver.LicenseClass, a.Driver.Rating, a.Driver.Phone, a.Driver.LicenseNumber
                })
                .ToListAsync(ct);

            return rows
                .GroupBy(r => r.VehicleId)
                .ToDictionary(g => g.Key, g =>
                {
                    var r = g.OrderByDescending(x => x.StartDate).First();
                    return new CurrentDriver(r.DriverId, r.FullName, r.LicenseClass, r.Rating, r.Phone, r.LicenseNumber ?? "", r.Id, r.StartDate);
                });
        }
    }
}
