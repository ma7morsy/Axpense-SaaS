using Axpense.Service.Common;
using Axpense.Service.Drivers.Dtos;

namespace Axpense.Service.Drivers
{
    public interface IDriverService
    {
        Task<DriverListResponse> ListAsync(Guid organizationId, DriverListQuery query, CancellationToken ct = default);
        Task<string> NextEmployeeNumberAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<DriverProfileDto>> GetProfileAsync(Guid organizationId, Guid driverId, CancellationToken ct = default);
        Task<ServiceResult<DriverProfileDto>> CreateAsync(Guid organizationId, Guid userId, DriverUpsertRequest request, CancellationToken ct = default);
        Task<ServiceResult<DriverProfileDto>> UpdateAsync(Guid organizationId, Guid userId, Guid driverId, DriverUpsertRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid driverId, CancellationToken ct = default);

        Task<ServiceResult<DriverDocumentDto>> AddDocumentAsync(Guid organizationId, Guid userId, Guid driverId, DriverDocumentCreateRequest request, FileUpload? file, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteDocumentAsync(Guid organizationId, Guid driverId, Guid documentId, CancellationToken ct = default);
        Task<ServiceResult<FileDownload>> OpenDocumentFileAsync(Guid organizationId, Guid driverId, Guid documentId, CancellationToken ct = default);
    }
}
