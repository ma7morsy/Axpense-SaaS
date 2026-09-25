using Axpense.Service.Common;
using Axpense.Service.Tracking.Dtos;

namespace Axpense.Service.Tracking
{
    public interface IFleetTrackingService
    {
        Task<FleetTrackingDto> GetFleetAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<TrackedVehicleDetailDto>> GetVehicleAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
    }
}
