namespace Axpense.Data.Entities.SettingsEntities
{
    /// <summary>Category for work-order tasks (Mechanical, Electrical…).</summary>
    public class TaskCategory : TenantEntity
    {
        public string Name { get; set; } = "";
        public int SortOrder { get; set; }
    }
}
