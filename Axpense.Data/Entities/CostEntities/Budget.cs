namespace Axpense.Data.Entities.CostEntities
{
    /// <summary>
    /// A monthly spending limit (Budgets page). Category is "Overall" (all spend) or an expense type;
    /// <see cref="ExpenseTypeId"/> is null for Overall. One limit per category per month.
    /// </summary>
    public class Budget : TenantEntity
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Overall";
        public Guid? ExpenseTypeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal LimitAmount { get; set; }
        public string? Notes { get; set; }
    }
}
