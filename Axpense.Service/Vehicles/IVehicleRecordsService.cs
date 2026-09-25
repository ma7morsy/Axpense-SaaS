using Axpense.Service.Common;
using Axpense.Service.Vehicles.Dtos;

namespace Axpense.Service.Vehicles
{
    /// <summary>The records behind the vehicle profile tabs. Every call is scoped to one vehicle of the tenant.</summary>
    public interface IVehicleRecordsService
    {
        // Parts
        Task<ServiceResult<List<VehiclePartDto>>> PartsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<VehiclePartDto>> AddPartAsync(Guid organizationId, Guid userId, Guid vehicleId, VehiclePartRequest request, CancellationToken ct = default);
        Task<ServiceResult<VehiclePartDto>> UpdatePartAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid partId, VehiclePartRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeletePartAsync(Guid organizationId, Guid vehicleId, Guid partId, CancellationToken ct = default);
        Task<ServiceResult<VehiclePartReplaceResult>> ReplacePartAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid partId, VehiclePartReplaceRequest request, CancellationToken ct = default);

        // Issues
        Task<ServiceResult<List<VehicleIssueDto>>> IssuesAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<VehicleIssueDto>> AddIssueAsync(Guid organizationId, Guid userId, Guid vehicleId, VehicleIssueRequest request, CancellationToken ct = default);
        Task<ServiceResult<VehicleIssueDto>> SetIssueStatusAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid issueId, VehicleIssueStatusRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteIssueAsync(Guid organizationId, Guid vehicleId, Guid issueId, CancellationToken ct = default);

        // Fuel
        Task<ServiceResult<List<FuelRecordDto>>> FuelAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<FuelRecordDto>> AddFuelAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, FuelRecordRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteFuelAsync(Guid organizationId, Guid vehicleId, Guid fuelId, CancellationToken ct = default);

        // Odometer
        Task<ServiceResult<List<OdometerReadingDto>>> ReadingsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<OdometerReadingDto>> AddReadingAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, OdometerReadingRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteReadingAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid readingId, CancellationToken ct = default);

        // Expenses
        Task<ServiceResult<List<VehicleExpenseDto>>> ExpensesAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<VehicleExpenseDto>> AddExpenseAsync(Guid organizationId, Guid userId, Guid vehicleId, VehicleExpenseRequest request, CancellationToken ct = default);
        Task<ServiceResult<VehicleExpenseDto>> UpdateExpenseAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid expenseId, VehicleExpenseRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteExpenseAsync(Guid organizationId, Guid vehicleId, Guid expenseId, CancellationToken ct = default);

        // Read-only history
        Task<ServiceResult<List<VehicleInspectionDto>>> InspectionsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
        Task<ServiceResult<List<VehicleAssignmentDto>>> AssignmentsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
    }
}
