namespace Axpense.Service.Settings
{
    // ---- Expense types
    public sealed record ExpenseTypeDto(Guid Id, string Name, string Color, bool IsSystem, int Entries, decimal Total);

    public sealed class ExpenseTypeRequest
    {
        public string Name { get; set; } = "";
        public string? Color { get; set; }
    }

    // ---- Task categories
    public sealed record TaskCategoryDto(Guid Id, string Name, int TaskCount);

    public sealed class TaskCategoryRequest
    {
        public string Name { get; set; } = "";
    }

    // ---- PM engine
    public sealed record EscalationStepDto(int AfterDaysOverdue, string Role);

    public sealed record PmEngineDto(
        int DistancePreAlertKm,
        int TimePreAlertDays,
        int EngineHourPreAlert,
        bool AutoGenerateWorkOrders,
        bool BlockDispatchOnCriticalOverdue,
        IReadOnlyList<EscalationStepDto> EscalationSteps,
        IReadOnlyList<string> Roles);

    public sealed class PmEngineRequest
    {
        public int DistancePreAlertKm { get; set; }
        public int TimePreAlertDays { get; set; }
        public int EngineHourPreAlert { get; set; }
        public bool AutoGenerateWorkOrders { get; set; }
        public bool BlockDispatchOnCriticalOverdue { get; set; }
        public List<EscalationStepDto> EscalationSteps { get; set; } = [];
    }

    // ---- Budget
    public sealed record BudgetMonthDto(int Month, decimal Amount, decimal SharePercent, decimal Actual, decimal? UtilizationPercent);

    public sealed record BudgetCategoryDto(Guid ExpenseTypeId, string Name, string Color, decimal SharePercent, decimal Budget, decimal Actual, decimal Remaining, decimal? UtilizationPercent);

    public sealed record BudgetDto(
        int Year,
        bool Exists,
        decimal Amount,
        decimal SuggestedAmount,
        decimal AllocatedTotal,
        decimal Unallocated,
        decimal ActualTotal,
        decimal SharePercentTotal,
        int? CurrentMonth,
        bool HasPriorYearSpend,
        IReadOnlyList<BudgetMonthDto> Months,
        IReadOnlyList<BudgetCategoryDto> Categories);

    public sealed class AmountRequest
    {
        public decimal Amount { get; set; }
    }

    public sealed class ShareRequest
    {
        public decimal SharePercent { get; set; }
    }
}
