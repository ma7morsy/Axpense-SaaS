namespace Axpense.Data.Entities.SettingsEntities
{
    /// <summary>
    /// An expense category (Fuel, Maintenance, Parts…). System types (SystemKey set) can't be deleted:
    /// "fuel" also counts fuel transactions, "maintenance" counts completed maintenance cost, and
    /// "other" collects any expense whose category no longer matches a type.
    /// </summary>
    public class ExpenseType : TenantEntity
    {
        public string Name { get; set; } = "";
        /// <summary>Hex colour used in charts and the budget allocation.</summary>
        public string Color { get; set; } = "#B8C4D0";
        public int SortOrder { get; set; }
        public string? SystemKey { get; set; }
    }
}
