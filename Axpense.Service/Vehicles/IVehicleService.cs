using Axpense.Service.Common;
using Axpense.Service.Drivers.Dtos;
using Axpense.Service.Vehicles.Dtos;

namespace Axpense.Service.Vehicles
{
    /// <summary>Vehicle register, profile, lifecycle (status, hand-off) and photo. Tenant-scoped.</summary>
    public interface IVehicleService
    {
        VehicleOptionsDto Options();
        Task<VehicleListResponse> ListAsync(Guid organizationId, VehicleListQuery query, CancellationToken ct = default);
        Task<ServiceResult<VehicleDetailDto>> GetAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<VehicleDetailDto>> CreateAsync(Guid organizationId, Guid userId, string userName, VehicleUpsertRequest request, CancellationToken ct = default);
        Task<ServiceResult<VehicleDetailDto>> UpdateAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, VehicleUpsertRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<VehicleDetailDto>> SetStatusAsync(Guid organizationId, Guid userId, Guid vehicleId, VehicleStatusRequest request, CancellationToken ct = default);
        /// <summary>Ends the current assignment on the effective date and (optionally) starts the new driver's.</summary>
        Task<ServiceResult<VehicleHandOffResult>> HandOffAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, VehicleHandOffRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> SetPhotoAsync(Guid organizationId, Guid userId, Guid vehicleId, FileUpload file, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeletePhotoAsync(Guid organizationId, Guid userId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<FileDownload>> OpenPhotoAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        /// <summary>Renewal and maintenance alerts across the fleet (feeds notifications).</summary>
        Task<List<(Guid VehicleId, string VehicleName, string Plate, Dtos.VehicleAlertDto Alert)>> FleetAlertsAsync(Guid organizationId, CancellationToken ct = default);
    }
}
