using Axpense.Data.Entities.CatalogEntities;

namespace Axpense.Data.Entities.VehicleEntities
{
    /// <summary>A defect reported on a vehicle (by a driver, an inspection or a technician). Shown as ISS-0001.</summary>
    public class VehicleIssue : TenantEntity
    {
        public Guid VehicleId { get; set; }
        public int Number { get; set; }
        public string Title { get; set; } = "";
        public string? Note { get; set; }
        /// <summary>Optional catalogue part the issue concerns.</summary>
        public Guid? PresetPartId { get; set; }
        public string Priority { get; set; } = Constants.VehicleConstants.PriorityMedium;
        public string Source { get; set; } = Constants.VehicleConstants.IssueSourceManual;
        public string Status { get; set; } = Constants.VehicleConstants.IssueOpen;
        public DateTime ReportedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        /// <summary>Inspection run that raised the issue (no FK: deleting the inspection keeps the issue).</summary>
        public Guid? SourceInspectionId { get; set; }

        public Vehicle Vehicle { get; set; } = null!;
        public PresetPart? PresetPart { get; set; }
    }
}
