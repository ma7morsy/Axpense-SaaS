namespace Axpense.Data.Entities.InspectionEntities
{
    /// <summary>
    /// One run of an inspection template against a vehicle. Starts "In progress" (a draft that keeps
    /// the answers) and becomes "Completed". The checklist is copied from the template when the run
    /// starts, so editing or deleting the template never changes history. Shown as INS-00001.
    /// </summary>
    public class Inspection : TenantEntity
    {
        public Guid VehicleId { get; set; }
        public int Number { get; set; }
        public Guid? TemplateId { get; set; }
        /// <summary>Template name at the time of the run.</summary>
        public string TemplateName { get; set; } = "";
        public DateTime InspectionDate { get; set; } = DateTime.UtcNow;
        /// <summary>Kept for the legacy list; equals the template name for template runs.</summary>
        public string Type { get; set; } = "Routine";
        public string Status { get; set; } = Constants.InspectionConstants.StatusInProgress;
        public decimal? Odometer { get; set; }
        public string? InspectorName { get; set; }
        public string? Notes { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? InspectorUserId { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public Vehicle Vehicle { get; set; } = null!;
        public InspectionTemplate? Template { get; set; }
        public ICollection<InspectionItem> Items { get; set; } = [];
    }
}
