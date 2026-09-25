namespace Axpense.Data.Entities.MaintenanceRecord
{
    /// <summary>
    /// A work order (WO-00001): a multi-task maintenance job on one vehicle. Stored statuses are
    /// Scheduled, In progress and Completed; "Overdue" is derived (still Scheduled after its scheduled date).
    /// EstimatedCost is the sum of the task costs; ActualCost is fixed when the order is completed.
    /// </summary>
    public class Maintenance : TenantEntity
    {
        public Guid VehicleId { get; set; }
        public int Number { get; set; }
        public string Type { get; set; } = Constants.MaintenanceConstants.TypePreventive;
        public string Priority { get; set; } = Constants.MaintenanceConstants.PriorityMedium;
        public string Status { get; set; } = Constants.MaintenanceConstants.StatusScheduled;
        public string Description { get; set; } = "";
        /// <summary>Scheduled date.</summary>
        public DateTime DueDate { get; set; }
        public Guid? TechnicianUserId { get; set; }
        public string? TechnicianName { get; set; }
        public decimal? EstimatedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public string? Notes { get; set; }

        /// <summary>Manual | Issue | Inspection | PM engine | Part replacement</summary>
        public string Source { get; set; } = Constants.MaintenanceConstants.SourceManual;
        /// <summary>Human reference of the source (ISS-0003, INS-00001, PRT-0004…).</summary>
        public string? SourceRef { get; set; }
        public Guid? IssueId { get; set; }
        public Guid? InspectionId { get; set; }
        public decimal? OdometerAtRaise { get; set; }

        public decimal? OdometerAtService { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public Vehicle Vehicle { get; set; } = null!;
        public ICollection<MaintenanceTask> Tasks { get; set; } = [];
    }

    /// <summary>A task line of a work order.</summary>
    public class MaintenanceTask : TenantEntity
    {
        public Guid MaintenanceId { get; set; }
        public int Sort { get; set; }
        public string Description { get; set; } = "";
        public Guid? PartCategoryId { get; set; }
        public string? PartCategoryName { get; set; }
        public Guid? TaskCategoryId { get; set; }
        public string? TaskCategoryName { get; set; }
        public decimal Cost { get; set; }
        public Maintenance Maintenance { get; set; } = null!;
    }
}
