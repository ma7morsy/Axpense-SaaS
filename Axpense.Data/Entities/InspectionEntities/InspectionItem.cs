namespace Axpense.Data.Entities.InspectionEntities
{
    /// <summary>A check inside an inspection run: a snapshot of the template check plus the inspector's answer.</summary>
    public class InspectionItem : TenantEntity
    {
        public Guid InspectionId { get; set; }
        public int Sort { get; set; }
        /// <summary>Check label.</summary>
        public string ChecklistItem { get; set; } = "";
        public Guid? PresetPartId { get; set; }
        public string? PartName { get; set; }
        public string FieldType { get; set; } = Constants.InspectionConstants.FieldPassFail;
        public bool Critical { get; set; }
        public string? Unit { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public bool IssueOnFail { get; set; } = true;

        /// <summary>pass | fail | na, or null while unanswered.</summary>
        public string? Result { get; set; }
        public bool Passed { get; set; }
        /// <summary>Gauge reading or 1–5 score.</summary>
        public decimal? Value { get; set; }
        /// <summary>Inspector's comment on a failure.</summary>
        public string? FailureReason { get; set; }
        public string? ImageUrl { get; set; }
        public string? PhotoKey { get; set; }
        public string? PhotoContentType { get; set; }
        /// <summary>Issue raised from this check on completion.</summary>
        public Guid? IssueId { get; set; }

        public Inspection Inspection { get; set; } = null!;
    }
}
