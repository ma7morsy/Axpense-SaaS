using System.Globalization;
using Axpense.Data.Constants;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Service.Common;
using Axpense.Service.Fleet;
using Axpense.Service.Settings;
using Axpense.Service.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Dashboard
{
    public sealed record KpiDto(decimal Value, decimal Previous, double? ChangePct);
    public sealed record MaintenanceDueKpiDto(int Due, int Overdue, int InProgress);
    public sealed record DashboardKpisDto(KpiDto Vehicles, MaintenanceDueKpiDto MaintenanceDue, KpiDto Fuel, KpiDto Maintenance, KpiDto Expenses,
        KpiDto Insurance, KpiDto Total);
    /// <summary>One of the four dashboard cost groups: fuel, maintenance, expenses (all other types) and insurance.</summary>
    public sealed record CostGroupDto(string Key, string Name, decimal Amount, decimal Previous, double Percent);
    /// <summary>An open work order, or a vehicle whose PM is due/overdue with no open work order yet (Id/Code null).</summary>
    public sealed record UpcomingMaintenanceDto(Guid? Id, string? Code, Guid VehicleId, string Plate, string Vehicle, string Task, string Type,
        DateTime? DueDate, int? Days, string? Label, bool Late, string Status, string Priority);
    public sealed record RecentExpenseDto(string Kind, string Category, Guid? VehicleId, string? Plate, string? Vendor, decimal Amount, DateTime Date);
    public sealed record DashboardSummaryDto(DateTime From, DateTime To, DateTime PreviousFrom, DateTime PreviousTo, string Currency,
        DashboardKpisDto Kpis, IReadOnlyList<CostGroupDto> Groups, IReadOnlyList<UpcomingMaintenanceDto> Upcoming, IReadOnlyList<RecentExpenseDto> RecentExpenses);

    public sealed record CostTrendPointDto(string Key, string Label, string Full, decimal Fuel, decimal Maintenance, decimal Expenses);

    public sealed record SeriesDto(Guid Id, string Name, string Color);
    public sealed record OperatingBucketDto(string Key, string Label, string Full, DateTime Start, DateTime End,
        IReadOnlyDictionary<string, decimal> Values, decimal Total, decimal PreviousTotal);
    public sealed record OperatingTrendDto(string Granularity, IReadOnlyList<SeriesDto> Series, IReadOnlyList<OperatingBucketDto> Buckets,
        decimal Total, decimal PreviousTotal, double? ChangePct);

    public sealed record BudgetRowDto(string Key, string Label, decimal Budget, decimal Actual, decimal Remaining, double? VariancePct, bool Over);
    public sealed record BudgetVsActualDto(string GroupBy, string Period, bool HasBudget, decimal Budget, decimal Actual, decimal Remaining,
        double? UtilizationPct, IReadOnlyList<BudgetRowDto> Rows);

    public sealed record RecurrenceRowDto(Guid? PartId, string Part, string? Category, int Times, int Vehicles, int Open, DateTime Last, bool Recurring);
    public sealed record RecurrenceDto(IReadOnlyList<RecurrenceRowDto> Rows, int TotalParts);

    public interface IDashboardService
    {
        Task<ServiceResult<DashboardSummaryDto>> SummaryAsync(Guid organizationId, DateTime? from, DateTime? to, CancellationToken ct = default);
        Task<ServiceResult<IReadOnlyList<CostTrendPointDto>>> CostTrendAsync(Guid organizationId, int months, CancellationToken ct = default);
        Task<ServiceResult<OperatingTrendDto>> OperatingTrendAsync(Guid organizationId, string? granularity, CancellationToken ct = default);
        Task<ServiceResult<BudgetVsActualDto>> BudgetAsync(Guid organizationId, string? groupBy, CancellationToken ct = default);
        Task<ServiceResult<RecurrenceDto>> RecurrenceAsync(Guid organizationId, int take, CancellationToken ct = default);
    }

    /// <summary>
    /// Dashboard figures. Spend has one definition everywhere (<see cref="SpendQueries"/>): expenses + fuel transactions +
    /// the actual cost of completed work orders, each mapped to an expense type. Rules:
    /// <list type="bullet">
    /// <item>Cost groups: Fuel (fuel type), Maintenance (maintenance type), Insurance (the type named "Insurance") and Expenses (every other type).
    /// Total fleet cost is the sum of all four.</item>
    /// <item>KPI changes compare the selected range with the equal-length period immediately before it.</item>
    /// <item>Maintenance due = open work orders plus vehicles whose PM is due/overdue with no open work order; overdue = past its date (or PM overdue).</item>
    /// <item>Operating trend: 12 months, 12 weeks or 30 days; each ghost bar is the same slot one whole window earlier.</item>
    /// <item>Budget vs actual: by month = the last 6 months' allocations; by category = annual budget × category share against the last 12 months.</item>
    /// <item>Recurrence: vehicle issues grouped by catalogue part; a part reported more than once is "Recurring".</item>
    /// </list>
    /// </summary>
    public sealed class DashboardService : IDashboardService
    {
        private readonly AxpenseDbContext _db;
        public DashboardService(AxpenseDbContext db) => _db = db;

        private static int Active => (int)CurrentStatusType.Active;
        private static DateTime Today => DateTime.UtcNow.Date;
        private const int MaxRangeDays = 800;
        private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

        private static double? Pct(decimal current, decimal previous) =>
            previous == 0 ? null : Math.Round((double)((current - previous) / previous * 100m), 1);

        private static KpiDto Kpi(decimal current, decimal previous) => new(current, previous, Pct(current, previous));

        private static ServiceResult<T> Invalid<T>(string field, string msg) => ServiceResult<T>.Invalid(new Dictionary<string, string> { [field] = msg });

        // ---- cost groups -------------------------------------------------
        private const string GFuel = "fuel", GMaint = "maintenance", GExp = "expenses", GIns = "insurance";

        private static Func<Guid?, string> Grouper(IReadOnlyList<ExpenseType> types)
        {
            var map = types.ToDictionary(t => t.Id, t =>
                t.SystemKey == SettingsDefaults.SystemFuel ? GFuel
                : t.SystemKey == SettingsDefaults.SystemMaintenance ? GMaint
                : string.Equals(t.Name.Trim(), "Insurance", StringComparison.OrdinalIgnoreCase) ? GIns : GExp);
            return id => id is { } g && map.TryGetValue(g, out var k) ? k : GExp;
        }

        // =====================================================================
        public async Task<ServiceResult<DashboardSummaryDto>> SummaryAsync(Guid organizationId, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var monthStart = new DateTime(Today.Year, Today.Month, 1);
            var f = (from ?? monthStart.AddMonths(-1)).Date;
            var t = (to ?? monthStart.AddDays(-1)).Date;
            if (t < f) return Invalid<DashboardSummaryDto>("to", "The end date must be on or after the start date.");
            if ((t - f).TotalDays > MaxRangeDays) return Invalid<DashboardSummaryDto>("from", $"Choose a range of {MaxRangeDays} days or fewer.");
            var toEx = t.AddDays(1);
            // Whole-month ranges compare with the same number of whole months before; other ranges with the same number of days.
            var wholeMonths = f.Day == 1 && toEx.Day == 1;
            var months = (toEx.Year - f.Year) * 12 + toEx.Month - f.Month;
            var prevFrom = wholeMonths ? f.AddMonths(-months) : f - (toEx - f);

            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var group = Grouper(types);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, prevFrom, toEx, ct);
            decimal Sum(string g, bool current) => spend.Where(r => (r.Date >= f) == current && group(r.ExpenseTypeId) == g).Sum(r => r.Amount);

            var vehicles = await _db.Vehicles.AsNoTracking()
                .Where(v => v.OrganizationId == organizationId && v.CurrentState == Active && v.Status != VehicleConstants.StatusRetired)
                .ToListAsync(ct);
            var upcoming = await UpcomingAsync(organizationId, vehicles, ct);
            var overdue = upcoming.Count(u => u.Late);
            var inProgress = upcoming.Count(u => u.Status == MaintenanceConstants.StatusInProgress);

            var names = new[] { (GFuel, "Fuel"), (GMaint, "Maintenance"), (GExp, "Expenses"), (GIns, "Insurance") };
            var total = spend.Where(r => r.Date >= f).Sum(r => r.Amount);
            var prevTotal = spend.Where(r => r.Date < f).Sum(r => r.Amount);
            var groups = names.Select(n => new CostGroupDto(n.Item1, n.Item2, Sum(n.Item1, true), Sum(n.Item1, false),
                total == 0 ? 0 : Math.Round((double)(Sum(n.Item1, true) / total * 100), 1))).ToList();

            var kpis = new DashboardKpisDto(
                // Fleet size is always today's; the comparison is with the fleet at the start of the range (purchase date, else date added).
                Kpi(vehicles.Count, vehicles.Count(v => (v.PurchaseDate ?? v.CreatedAt) < f)),
                new MaintenanceDueKpiDto(upcoming.Count, overdue, inProgress),
                Kpi(groups[0].Amount, groups[0].Previous), Kpi(groups[1].Amount, groups[1].Previous),
                Kpi(groups[2].Amount, groups[2].Previous), Kpi(groups[3].Amount, groups[3].Previous),
                Kpi(total, prevTotal));

            var recent = await RecentAsync(organizationId, types, ct);
            var currency = await _db.OrganizationSettings.AsNoTracking().Where(s => s.OrganizationId == organizationId)
                .Select(s => s.Currency).FirstOrDefaultAsync(ct) ?? "EGP";
            return ServiceResult<DashboardSummaryDto>.Ok(new DashboardSummaryDto(f, t, prevFrom, f.AddDays(-1), currency, kpis, groups,
                upcoming.Take(6).ToList(), recent));
        }

        private async Task<List<UpcomingMaintenanceDto>> UpcomingAsync(Guid organizationId, List<Data.Entities.Vehicle> vehicles, CancellationToken ct)
        {
            var open = await _db.MaintenanceRecords.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && m.CurrentState == Active && m.Status != MaintenanceConstants.StatusCompleted)
                .Select(m => new
                {
                    m.Id, m.Number, m.VehicleId, m.Vehicle.PlateNumber, m.Vehicle.Make, m.Vehicle.Model, m.Description, m.Type, m.DueDate, m.Status, m.Priority,
                    Task = m.Tasks.OrderBy(x => x.Sort).Select(x => x.Description).FirstOrDefault()
                })
                .ToListAsync(ct);
            var rows = open.Select(m =>
            {
                var days = (int)(m.DueDate.Date - Today).TotalDays;
                var late = m.Status == MaintenanceConstants.StatusScheduled && days < 0;
                return new UpcomingMaintenanceDto(m.Id, WorkOrderService.Code(m.Number), m.VehicleId, m.PlateNumber, $"{m.Make} {m.Model}".Trim(),
                    string.IsNullOrWhiteSpace(m.Task) ? m.Description : m.Task!, m.Type, m.DueDate, days, null, late,
                    late ? MaintenanceConstants.StatusOverdue : m.Status, m.Priority);
            }).ToList();

            var withOrder = open.Select(m => m.VehicleId).ToHashSet();
            var rules = await PmRules.LoadAsync(_db, organizationId, ct);
            foreach (var v in vehicles.Where(v => !withOrder.Contains(v.Id)))
            {
                var pm = PmCalculator.Evaluate(v, Today, rules);
                if (pm.Status is not ("due" or "overdue")) continue;
                var late = pm.Status == "overdue";
                int? days = pm.Unit == "days" ? (late ? -(pm.DaysOverdue ?? 0) : int.TryParse(pm.Label.Split(' ')[0].Replace(",", ""), out var d) ? d : null) : null;
                rows.Add(new UpcomingMaintenanceDto(null, null, v.Id, v.PlateNumber, $"{v.Make} {v.Model}".Trim(), "Preventive service",
                    MaintenanceConstants.TypePreventive, days is null ? null : Today.AddDays(days.Value), days, days is null ? pm.Label : null, late,
                    late ? MaintenanceConstants.StatusOverdue : "PM due", late ? MaintenanceConstants.PriorityHigh : MaintenanceConstants.PriorityMedium));
            }
            // Late first, then soonest; rows without a day count (distance legs) sit after this week's items.
            return rows.OrderBy(r => r.Days ?? (r.Late ? -1 : 5)).ThenByDescending(r => Array.IndexOf(MaintenanceConstants.Priorities, r.Priority)).ToList();
        }

        private async Task<List<RecentExpenseDto>> RecentAsync(Guid organizationId, List<ExpenseType> types, CancellationToken ct)
        {
            const int take = 5;
            var today = Today.AddDays(1);
            var byName = types.GroupBy(t => t.Name.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Name);
            var fuelName = types.FirstOrDefault(t => t.SystemKey == SettingsDefaults.SystemFuel)?.Name ?? "Fuel";
            var maintName = types.FirstOrDefault(t => t.SystemKey == SettingsDefaults.SystemMaintenance)?.Name ?? "Maintenance";

            var expenses = await _db.Expenses.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.ExpenseDate < today)
                .OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.CreatedAt).Take(take)
                .Select(x => new { x.Category, x.VehicleId, Plate = x.Vehicle != null ? x.Vehicle.PlateNumber : null, x.Vendor, x.Amount, x.ExpenseDate, x.CreatedAt })
                .ToListAsync(ct);
            var fuel = await _db.FuelTransactions.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.TransactionDate < today)
                .OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt).Take(take)
                .Select(x => new { x.VehicleId, x.Vehicle.PlateNumber, x.Station, x.TotalAmount, x.TransactionDate, x.CreatedAt })
                .ToListAsync(ct);
            var orders = await _db.MaintenanceRecords.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.CompletedAtUtc != null && x.ActualCost != null && x.CompletedAtUtc < today)
                .OrderByDescending(x => x.CompletedAtUtc).Take(take)
                .Select(x => new { x.VehicleId, x.Vehicle.PlateNumber, x.TechnicianName, Amount = x.ActualCost!.Value, Date = x.CompletedAtUtc!.Value })
                .ToListAsync(ct);

            var rows = new List<(RecentExpenseDto Row, DateTime Sort)>();
            rows.AddRange(expenses.Select(x => (new RecentExpenseDto("expense",
                byName.TryGetValue((x.Category ?? "").ToLowerInvariant(), out var n) ? n : "Other", x.VehicleId, x.Plate, x.Vendor, x.Amount, x.ExpenseDate.Date), x.CreatedAt)));
            rows.AddRange(fuel.Select(x => (new RecentExpenseDto("fuel", fuelName, x.VehicleId, x.PlateNumber, x.Station, x.TotalAmount, x.TransactionDate.Date), x.CreatedAt)));
            rows.AddRange(orders.Select(x => (new RecentExpenseDto("maintenance", maintName, x.VehicleId, x.PlateNumber, x.TechnicianName, x.Amount, x.Date.Date), x.Date)));
            return rows.OrderByDescending(r => r.Row.Date).ThenByDescending(r => r.Sort).Take(take).Select(r => r.Row).ToList();
        }

        // =====================================================================
        /// <summary>The last N complete months (the running month is excluded, as in the KPI ranges).</summary>
        public async Task<ServiceResult<IReadOnlyList<CostTrendPointDto>>> CostTrendAsync(Guid organizationId, int months, CancellationToken ct = default)
        {
            if (months is not (3 or 6 or 12)) return Invalid<IReadOnlyList<CostTrendPointDto>>("months", "Choose 3, 6 or 12 months.");
            var end = new DateTime(Today.Year, Today.Month, 1);
            var first = end.AddMonths(-months);
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var group = Grouper(types);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, first, end, ct);
            var points = Enumerable.Range(0, months).Select(i =>
            {
                var s = first.AddMonths(i);
                var rows = spend.Where(r => r.Date >= s && r.Date < s.AddMonths(1)).ToList();
                decimal G(string g) => rows.Where(r => group(r.ExpenseTypeId) == g).Sum(r => r.Amount);
                return new CostTrendPointDto(s.ToString("yyyy-MM"), s.ToString("MMM", Gb), s.ToString("MMMM yyyy", Gb), G(GFuel), G(GMaint), G(GExp));
            }).ToList();
            return ServiceResult<IReadOnlyList<CostTrendPointDto>>.Ok(points);
        }

        // =====================================================================
        public async Task<ServiceResult<OperatingTrendDto>> OperatingTrendAsync(Guid organizationId, string? granularity, CancellationToken ct = default)
        {
            var g = (granularity ?? "month").ToLowerInvariant();
            List<(DateTime S, DateTime E, string Label, string Full)> buckets;
            int shiftDays = 0;
            if (g == "month")
            {
                var m0 = new DateTime(Today.Year, Today.Month, 1);
                buckets = Enumerable.Range(0, 12).Select(i => m0.AddMonths(i - 11)).Select((s, i) =>
                    (s, s.AddMonths(1), s.ToString("MMM", Gb) + (i == 0 || s.Month == 1 ? " " + s.ToString("yy") : ""), s.ToString("MMMM yyyy", Gb))).ToList();
            }
            else if (g == "week")
            {
                var monday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
                buckets = Enumerable.Range(0, 12).Select(i => monday.AddDays(7 * (i - 11)))
                    .Select(s => (s, s.AddDays(7), s.ToString("dd MMM", Gb), "Week of " + s.ToString("dd MMM yyyy", Gb))).ToList();
                shiftDays = 12 * 7;
            }
            else if (g == "day")
            {
                buckets = Enumerable.Range(0, 30).Select(i => Today.AddDays(i - 29))
                    .Select(s => (s, s.AddDays(1), s.ToString("dd", Gb), s.ToString("dd MMM yyyy", Gb))).ToList();
                shiftDays = 30;
            }
            else return Invalid<OperatingTrendDto>("granularity", "Choose monthly, weekly or daily.");

            // Ghost bar: the same slot one whole window earlier.
            DateTime Back(DateTime d) => g == "month" ? d.AddMonths(-12) : d.AddDays(-shiftDays);

            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, Back(buckets[0].S), buckets[^1].E, ct);
            var result = buckets.Select(b =>
            {
                var rows = spend.Where(r => r.Date >= b.S && r.Date < b.E).ToList();
                var values = types.ToDictionary(t => t.Id.ToString(), t => rows.Where(r => r.ExpenseTypeId == t.Id).Sum(r => r.Amount));
                var previous = spend.Where(r => r.Date >= Back(b.S) && r.Date < Back(b.E)).Sum(r => r.Amount);
                return new OperatingBucketDto(b.S.ToString("yyyy-MM-dd"), b.Label, b.Full, b.S, b.E.AddDays(-1), values, rows.Sum(r => r.Amount), previous);
            }).ToList();
            var total = result.Sum(b => b.Total);
            var prevTotal = result.Sum(b => b.PreviousTotal);
            return ServiceResult<OperatingTrendDto>.Ok(new OperatingTrendDto(g, types.Select(t => new SeriesDto(t.Id, t.Name, t.Color)).ToList(),
                result, total, prevTotal, Pct(total, prevTotal)));
        }

        // =====================================================================
        public async Task<ServiceResult<BudgetVsActualDto>> BudgetAsync(Guid organizationId, string? groupBy, CancellationToken ct = default)
        {
            var g = (groupBy ?? "month").ToLowerInvariant();
            if (g is not ("month" or "category")) return Invalid<BudgetVsActualDto>("groupBy", "Group by month or category.");
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var m0 = new DateTime(Today.Year, Today.Month, 1);
            var from = g == "month" ? m0.AddMonths(-5) : Today.AddDays(1).AddYears(-1);
            var toEx = g == "month" ? m0.AddMonths(1) : Today.AddDays(1);
            var years = Enumerable.Range(from.Year, toEx.AddDays(-1).Year - from.Year + 1).ToList();
            var budgets = await _db.AnnualBudgets.AsNoTracking().Include(b => b.Months).Include(b => b.CategoryShares)
                .Where(b => b.OrganizationId == organizationId && b.CurrentState == Active && years.Contains(b.Year)).ToListAsync(ct);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, from, toEx, ct);

            static decimal MonthBudget(AnnualBudget? b, int month) =>
                b is null ? 0 : b.Months.FirstOrDefault(x => x.Month == month)?.Amount ?? Math.Round(b.Amount / 12, 2);

            List<(string Key, string Label, decimal Budget, decimal Actual)> raw;
            string period;
            if (g == "month")
            {
                raw = Enumerable.Range(0, 6).Select(i => m0.AddMonths(i - 5)).Select(s => (s.ToString("yyyy-MM"), s.ToString("MMM yyyy", Gb),
                    MonthBudget(budgets.FirstOrDefault(b => b.Year == s.Year), s.Month),
                    spend.Where(r => r.Date >= s && r.Date < s.AddMonths(1)).Sum(r => r.Amount))).ToList();
                period = $"{from:MMM yyyy} – {m0:MMM yyyy}";
            }
            else
            {
                var current = budgets.FirstOrDefault(b => b.Year == Today.Year);
                var shares = current?.CategoryShares.ToDictionary(s => s.ExpenseTypeId, s => s.SharePercent) ?? [];
                raw = types.Select(t => (t.Id.ToString(), t.Name, Math.Round((current?.Amount ?? 0) * shares.GetValueOrDefault(t.Id) / 100, 2),
                    spend.Where(r => r.ExpenseTypeId == t.Id).Sum(r => r.Amount))).ToList();
                period = $"{from:dd MMM yyyy} – {Today:dd MMM yyyy}";
            }
            var rows = raw.Select(r => new BudgetRowDto(r.Key, r.Label, r.Budget, r.Actual, r.Budget - r.Actual,
                r.Budget > 0 ? Math.Round((double)((r.Actual - r.Budget) / r.Budget * 100), 1) : null, r.Budget > 0 && r.Actual > r.Budget)).ToList();
            var budget = rows.Sum(r => r.Budget);
            var actual = rows.Sum(r => r.Actual);
            return ServiceResult<BudgetVsActualDto>.Ok(new BudgetVsActualDto(g, period, budgets.Count > 0 && budget > 0, budget, actual, budget - actual,
                budget > 0 ? Math.Round((double)(actual / budget * 100), 1) : null, rows));
        }

        // =====================================================================
        public async Task<ServiceResult<RecurrenceDto>> RecurrenceAsync(Guid organizationId, int take, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);
            var issues = await _db.VehicleIssues.AsNoTracking()
                .Where(i => i.OrganizationId == organizationId && i.CurrentState == Active)
                .Select(i => new { i.PresetPartId, Part = i.PresetPart != null ? i.PresetPart.Name : null, Category = i.PresetPart != null ? i.PresetPart.Category.Name : null,
                    i.VehicleId, i.Status, i.ReportedDate })
                .ToListAsync(ct);
            var rows = issues.GroupBy(i => i.PresetPartId).Select(gr => new RecurrenceRowDto(gr.Key, gr.First().Part ?? "Unclassified", gr.First().Category,
                    gr.Count(), gr.Select(x => x.VehicleId).Distinct().Count(), gr.Count(x => x.Status != VehicleConstants.IssueResolved),
                    gr.Max(x => x.ReportedDate), gr.Count() > 1))
                .OrderByDescending(r => r.Times).ThenByDescending(r => r.Open).ThenBy(r => r.Part)
                .ToList();
            return ServiceResult<RecurrenceDto>.Ok(new RecurrenceDto(rows.Take(take).ToList(), rows.Count));
        }
    }
}
