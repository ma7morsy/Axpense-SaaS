namespace Axpense.Service.WorkOrders
{
    public sealed record WorkOrderTaskDto(Guid Id, int Sort, string Description, Guid? PartCategoryId, string? PartCategoryName,
        Guid? TaskCategoryId, string? TaskCategoryName, decimal Cost);

    public sealed record WorkOrderListItemDto(
        Guid Id, string Code, Guid VehicleId, string VehicleName, string PlateNumber, string Type, string Priority,
        string Status, string DisplayStatus, int? DaysOverdue, DateTime ScheduledDate, string? TechnicianName, decimal Total, int TaskCount,
        string Description, string Source, string? SourceRef);

    public sealed record WorkOrderDetailDto(
        Guid Id, string Code, Guid VehicleId, string VehicleName, string PlateNumber, string ReadingUnit, string Type, string Priority,
        string Status, string DisplayStatus, int? DaysOverdue, string? NextStatus, DateTime ScheduledDate, Guid? TechnicianUserId, string? TechnicianName,
        string Description, string? Notes, string Source, string? SourceRef, Guid? IssueId, Guid? InspectionId,
        decimal? OdometerAtRaise, decimal? OdometerAtService, decimal Total, decimal? ActualCost,
        DateTime? StartedAtUtc, DateTime? CompletedAtUtc, IReadOnlyList<WorkOrderTaskDto> Tasks);

    public sealed record WorkOrderStatsDto(int ScheduledNext30Days, int InProgress, int Overdue, string? OverdueEscalatedTo, decimal CommittedCost, int OpenIssues);

    public sealed record WorkOrderListResponse(IReadOnlyList<WorkOrderListItemDto> Items, int Total, WorkOrderStatsDto Stats);

    public sealed class WorkOrderListQuery
    {
        /// <summary>Scheduled | In progress | Overdue | Completed</summary>
        public string? Status { get; set; }
        public string? Type { get; set; }
        public Guid? VehicleId { get; set; }
    }

    public sealed class WorkOrderTaskRequest
    {
        public string? Description { get; set; }
        public Guid? PartCategoryId { get; set; }
        public Guid? TaskCategoryId { get; set; }
        public decimal? Cost { get; set; }
    }

    public sealed class WorkOrderRequest
    {
        public Guid? VehicleId { get; set; }
        public string? Type { get; set; }
        public string? Priority { get; set; }
        public string? Status { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public Guid? TechnicianUserId { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public List<WorkOrderTaskRequest> Tasks { get; set; } = [];
        /// <summary>Optional issue this order fixes (moves it to "In progress"; resolved on completion).</summary>
        public Guid? IssueId { get; set; }
    }

    public sealed class WorkOrderStatusRequest
    {
        public string Status { get; set; } = "";
        /// <summary>Reading at service, used when completing.</summary>
        public decimal? Odometer { get; set; }
    }

    public sealed record NamedOption(Guid Id, string Name);

    public sealed record WorkOrderOptionsDto(
        IReadOnlyList<string> Types, IReadOnlyList<string> Priorities, IReadOnlyList<string> Statuses, IReadOnlyList<string> FilterStatuses,
        IReadOnlyList<NamedOption> Technicians, IReadOnlyList<NamedOption> PartCategories, IReadOnlyList<NamedOption> TaskCategories);

    public sealed record PmEngineRunResult(int Created, int AlreadyOpen, IReadOnlyList<string> Codes);
    public sealed record PmEngineRunRequest(IReadOnlyList<Guid>? VehicleIds);

    public sealed record PmPreviewRowDto(Guid VehicleId, string VehicleName, string PlateNumber, string Trigger, string LeadKind, decimal Interval, string Unit,
        string Status, string Label, Guid? TaskId, string TaskName, string Role, decimal? DurationHours, decimal EstimatedCost,
        Guid? OpenOrderId, string? OpenOrderCode, bool WillCreate, bool DispatchBlocked);

    public sealed record PmEnginePreviewDto(int DistancePreAlertKm, int TimePreAlertDays, int EngineHourPreAlert, bool AutoWorkOrders, bool BlockDispatch,
        IReadOnlyList<PmPreviewRowDto> Rows, int ToCreate, int DispatchBlocked, decimal EstimatedTotal);

    public sealed record FleetIssueDto(Guid Id, string Code, Guid VehicleId, string VehicleName, string PlateNumber, string Title, string? Note,
        string? PartName, string Priority, string Source, string Status, DateTime ReportedDate, Guid? WorkOrderId, string? WorkOrderCode);
}
