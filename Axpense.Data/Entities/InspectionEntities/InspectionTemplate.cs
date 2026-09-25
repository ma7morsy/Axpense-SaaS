using Axpense.Data.Entities.CatalogEntities;

namespace Axpense.Data.Entities.InspectionEntities
{
    /// <summary>A reusable checklist. Scope is "All" or a vehicle category; only active templates can be run.</summary>
    public class InspectionTemplate : TenantEntity
    {
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public string Scope { get; set; } = Constants.InspectionConstants.ScopeAll;
        public bool Active { get; set; } = true;
        /// <summary>Set for built-in templates (e.g. "handoff"): they can be edited but not deleted or deactivated.</summary>
        public string? SystemKey { get; set; }
        public ICollection<InspectionTemplateItem> Items { get; set; } = [];
    }

    /// <summary>A check of a template, always linked to a part of the catalogue.</summary>
    public class InspectionTemplateItem : TenantEntity
    {
        public Guid TemplateId { get; set; }
        public int Sort { get; set; }
        public string Label { get; set; } = "";
        public Guid PresetPartId { get; set; }
        public string FieldType { get; set; } = Constants.InspectionConstants.FieldPassFail;
        public bool Critical { get; set; }
        public string? Unit { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public bool IssueOnFail { get; set; } = true;

        public InspectionTemplate Template { get; set; } = null!;
        public PresetPart PresetPart { get; set; } = null!;
    }
}
