namespace Axpense.Data.Entities.SettingsEntities
{
    /// <summary>
    /// PM task library entry (Settings → Maintenance): the job the PM engine puts on a preventive work order
    /// when a vehicle's interval is due. Intervals are informational for matching: a task with an interval in the
    /// unit of the vehicle's leading PM leg (km / days / hours) is preferred.
    /// </summary>
    public class PmTask : TenantEntity
    {
        public string Name { get; set; } = "";
        public Guid? PartCategoryId { get; set; }
        public Guid? TaskCategoryId { get; set; }
        /// <summary>usage | time | hours | hybrid</summary>
        public string Trigger { get; set; } = "usage";
        public int? IntervalKm { get; set; }
        public int? IntervalDays { get; set; }
        public int? IntervalHours { get; set; }
        public decimal DurationHours { get; set; } = 1;
        public decimal EstimatedCost { get; set; }
        public string Role { get; set; } = "Technician";
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public CatalogEntities.PartCategory? PartCategory { get; set; }
        public TaskCategory? TaskCategory { get; set; }
    }
}
