namespace Axpense.Data.Entities.SettingsEntities
{
    /// <summary>The organization's spend budget for one calendar year, with its monthly plan and category split.</summary>
    public class AnnualBudget : TenantEntity
    {
        public int Year { get; set; }
        public decimal Amount { get; set; }

        public ICollection<BudgetMonthAllocation> Months { get; set; } = [];
        public ICollection<BudgetCategoryShare> CategoryShares { get; set; } = [];
    }

    public class BudgetMonthAllocation : TenantEntity
    {
        public Guid AnnualBudgetId { get; set; }
        /// <summary>1 – 12.</summary>
        public int Month { get; set; }
        public decimal Amount { get; set; }

        public AnnualBudget AnnualBudget { get; set; } = null!;
    }

    public class BudgetCategoryShare : TenantEntity
    {
        public Guid AnnualBudgetId { get; set; }
        public Guid ExpenseTypeId { get; set; }
        /// <summary>Percent of the annual budget, 0 – 100 (one decimal).</summary>
        public decimal SharePercent { get; set; }

        public AnnualBudget AnnualBudget { get; set; } = null!;
        public ExpenseType ExpenseType { get; set; } = null!;
    }
}
