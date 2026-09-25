using System.Globalization;
using Axpense.Data.Constants;
using Axpense.Data.Entities.CostEntities;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Common;
using Axpense.Service.Settings;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Budgets
{
    public sealed record BudgetCategoryOption(string Value, string Label, string? Color);
    public sealed record BudgetOptionsDto(IReadOnlyList<BudgetCategoryOption> Categories, int MinYear, int MaxYear);

    public sealed record BudgetRowDto(Guid Id, string Name, string Category, Guid? ExpenseTypeId, string? Color, int Year, int Month, string Period,
        decimal LimitAmount, decimal Actual, decimal Remaining, double UtilizationPct, string Status, decimal? Projected, string? Notes);

    public sealed record BudgetTotalsDto(decimal Budgeted, decimal Actual, decimal Remaining, double? UtilizationPct, int Count, int Over, int AtRisk, string Basis);

    public sealed record BudgetListDto(int Year, int? Month, BudgetTotalsDto Totals, IReadOnlyList<BudgetRowDto> Rows);

    public sealed record BudgetBreakdownDto(string Name, string Color, decimal Amount, double Percent);
    public sealed record BudgetDayDto(string Date, decimal Amount, decimal Cumulative);
    public sealed record BudgetEntryDto(string Date, string Kind, string Description, string? Plate, decimal Amount);

    public sealed record BudgetDetailDto(BudgetRowDto Budget, IReadOnlyList<BudgetBreakdownDto> Breakdown, IReadOnlyList<BudgetDayDto> Daily,
        IReadOnlyList<BudgetEntryDto> TopEntries, int DaysInMonth, int DaysElapsed, decimal DailyAllowance);

    public sealed record BudgetRequest(string? Name, string? Category, int? Year, int? Month, decimal? LimitAmount, string? Notes);

    public interface IBudgetLimitService
    {
        Task<BudgetOptionsDto> OptionsAsync(Guid organizationId, CancellationToken ct = default);
        Task<BudgetListDto> ListAsync(Guid organizationId, int year, int? month, CancellationToken ct = default);
        Task<ServiceResult<BudgetDetailDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);
        Task<ServiceResult<BudgetRowDto>> CreateAsync(Guid organizationId, Guid userId, BudgetRequest request, CancellationToken ct = default);
        Task<ServiceResult<BudgetRowDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, BudgetRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    }

    /// <summary>
    /// Monthly spending limits. Rules:
    /// <list type="bullet">
    /// <item>Name 1–100 characters; category "Overall" or an active expense type; limit 1–1,000,000,000; year 2000–2100; month 1–12.</item>
    /// <item>One limit per category per month (409 BUDGET_EXISTS).</item>
    /// <item>Actual spend uses the dashboard definition (expenses + fuel + completed work orders) for that calendar month,
    /// all spend for Overall, otherwise only the category's expense type.</item>
    /// <item>Status: Over (&gt; 100 %), At risk (≥ 80 % used, or the current month is projected above the limit at its run rate), On track.</item>
    /// <item>Period totals: when an Overall limit exists for a month it is the month's budget; otherwise the category limits are added up
    /// (so the same spend is never counted twice).</item>
    /// </list>
    /// </summary>
    public sealed class BudgetLimitService : IBudgetLimitService
    {
        public const string Overall = "Overall";
        private readonly AxpenseDbContext _db;
        public BudgetLimitService(AxpenseDbContext db) => _db = db;
        private static int Active => (int)CurrentStatusType.Active;
        private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

        public async Task<BudgetOptionsDto> OptionsAsync(Guid organizationId, CancellationToken ct = default)
        {
            await OrganizationDefaultsSeeder.SeedExpenseTypesAsync(_db, organizationId, ct);
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var cats = new List<BudgetCategoryOption> { new(Overall, "Overall (all spend)", "#0F2A44") };
            cats.AddRange(types.Select(t => new BudgetCategoryOption(t.Name, t.Name, t.Color)));
            return new BudgetOptionsDto(cats, 2000, 2100);
        }

        private static string Status(decimal limit, decimal actual, decimal? projected)
        {
            if (limit <= 0) return "On track";
            if (actual > limit) return "Over";
            if (actual >= limit * 0.8m || projected > limit) return "At risk";
            return "On track";
        }

        private static decimal? Projection(int year, int month, decimal actual)
        {
            var today = DateTime.UtcNow.Date;
            if (today.Year != year || today.Month != month) return null;
            var days = DateTime.DaysInMonth(year, month);
            return Math.Round(actual / today.Day * days, 2);
        }

        private BudgetRowDto Row(Budget b, IReadOnlyList<ExpenseType> types, IReadOnlyList<SpendRow> spend)
        {
            var start = new DateTime(b.Year, b.Month, 1);
            var end = start.AddMonths(1);
            var actual = spend.Where(r => r.Date >= start && r.Date < end && (b.ExpenseTypeId == null || r.ExpenseTypeId == b.ExpenseTypeId)).Sum(r => r.Amount);
            var projected = Projection(b.Year, b.Month, actual);
            var color = b.ExpenseTypeId is null ? "#0F2A44" : types.FirstOrDefault(t => t.Id == b.ExpenseTypeId)?.Color;
            var util = b.LimitAmount > 0 ? Math.Round((double)(actual / b.LimitAmount * 100), 1) : 0;
            return new BudgetRowDto(b.Id, b.Name, b.Category, b.ExpenseTypeId, color, b.Year, b.Month, start.ToString("MMM yyyy", Gb),
                b.LimitAmount, actual, b.LimitAmount - actual, util, Status(b.LimitAmount, actual, projected), projected, b.Notes);
        }

        public async Task<BudgetListDto> ListAsync(Guid organizationId, int year, int? month, CancellationToken ct = default)
        {
            var budgets = await _db.Budgets.AsNoTracking()
                .Where(b => b.OrganizationId == organizationId && b.CurrentState == Active && b.Year == year && (month == null || b.Month == month))
                .ToListAsync(ct);
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var from = new DateTime(year, month ?? 1, 1);
            var to = month is null ? from.AddYears(1) : from.AddMonths(1);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, from, to, ct);
            var rows = budgets.Select(b => Row(b, types, spend))
                .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ThenBy(r => r.ExpenseTypeId == null ? 0 : 1).ThenBy(r => r.Category).ToList();

            // Totals per month: Overall limit when present, else the sum of category limits (no double counting).
            decimal budgeted = 0, actual = 0;
            var basis = new HashSet<string>();
            foreach (var g in rows.GroupBy(r => (r.Year, r.Month)))
            {
                var overall = g.FirstOrDefault(r => r.ExpenseTypeId == null);
                if (overall is not null) { budgeted += overall.LimitAmount; actual += overall.Actual; basis.Add("overall"); }
                else { budgeted += g.Sum(r => r.LimitAmount); actual += g.Sum(r => r.Actual); basis.Add("categories"); }
            }
            var totals = new BudgetTotalsDto(budgeted, actual, budgeted - actual, budgeted > 0 ? Math.Round((double)(actual / budgeted * 100), 1) : null,
                rows.Count, rows.Count(r => r.Status == "Over"), rows.Count(r => r.Status == "At risk"),
                basis.Count == 0 ? "none" : basis.Count == 2 ? "mixed" : basis.First());
            return new BudgetListDto(year, month, totals, rows);
        }

        public async Task<ServiceResult<BudgetDetailDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            var b = await _db.Budgets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (b is null) return ServiceResult<BudgetDetailDto>.NotFound("Budget not found.");
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var start = new DateTime(b.Year, b.Month, 1);
            var end = start.AddMonths(1);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, start, end, ct);
            var row = Row(b, types, spend);
            var mine = spend.Where(r => b.ExpenseTypeId == null || r.ExpenseTypeId == b.ExpenseTypeId).ToList();
            var total = mine.Sum(r => r.Amount);

            var breakdown = b.ExpenseTypeId is null
                ? types.Select(t => new { t, a = mine.Where(r => r.ExpenseTypeId == t.Id).Sum(r => r.Amount) }).Where(x => x.a > 0).OrderByDescending(x => x.a)
                    .Select(x => new BudgetBreakdownDto(x.t.Name, x.t.Color, x.a, total == 0 ? 0 : Math.Round((double)(x.a / total * 100), 1))).ToList()
                : new List<BudgetBreakdownDto>
                {
                    new("Expenses", "#74BCF3", mine.Where(r => r.Source == SpendSource.Expense).Sum(r => r.Amount), 0),
                    new("Fuel", "#19B394", mine.Where(r => r.Source == SpendSource.Fuel).Sum(r => r.Amount), 0),
                    new("Work orders", "#1F7BD8", mine.Where(r => r.Source == SpendSource.WorkOrder).Sum(r => r.Amount), 0),
                }.Where(x => x.Amount > 0).Select(x => x with { Percent = total == 0 ? 0 : Math.Round((double)(x.Amount / total * 100), 1) }).ToList();

            var days = DateTime.DaysInMonth(b.Year, b.Month);
            decimal cum = 0;
            var daily = Enumerable.Range(1, days).Select(d =>
            {
                var date = new DateTime(b.Year, b.Month, d);
                var a = mine.Where(r => r.Date.Date == date).Sum(r => r.Amount);
                cum += a;
                return new BudgetDayDto(date.ToString("yyyy-MM-dd"), a, cum);
            }).ToList();
            var today = DateTime.UtcNow.Date;
            var elapsed = today < start ? 0 : today >= end ? days : today.Day;

            var entries = await TopEntriesAsync(organizationId, b, types, start, end, ct);
            return ServiceResult<BudgetDetailDto>.Ok(new BudgetDetailDto(row, breakdown, daily, entries, days, elapsed, Math.Round(b.LimitAmount / days, 2)));
        }

        private async Task<List<BudgetEntryDto>> TopEntriesAsync(Guid organizationId, Budget b, IReadOnlyList<ExpenseType> types, DateTime start, DateTime end, CancellationToken ct)
        {
            var type = types.FirstOrDefault(t => t.Id == b.ExpenseTypeId);
            var isFuel = type?.SystemKey == SettingsDefaults.SystemFuel;
            var isMaint = type?.SystemKey == SettingsDefaults.SystemMaintenance;
            var isOther = type?.SystemKey == SettingsDefaults.SystemOther;
            var names = types.Select(t => t.Name.ToLower()).ToList();
            var list = new List<BudgetEntryDto>();

            if (type is null || !isFuel && !isMaint || isOther)
            {
                var catName = type?.Name.ToLower();
                var q = _db.Expenses.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.ExpenseDate >= start && x.ExpenseDate < end);
                if (type is not null)
                    q = isOther ? q.Where(x => x.Category.ToLower() == catName || !names.Contains(x.Category.ToLower())) : q.Where(x => x.Category.ToLower() == catName);
                list.AddRange(await q.OrderByDescending(x => x.Amount).Take(8)
                    .Select(x => new BudgetEntryDto(x.ExpenseDate.ToString("yyyy-MM-dd"), x.Category, x.Description, x.Vehicle != null ? x.Vehicle.PlateNumber : null, x.Amount)).ToListAsync(ct));
            }
            if (type is null || isFuel)
                list.AddRange(await _db.FuelTransactions.AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.TransactionDate >= start && x.TransactionDate < end)
                    .OrderByDescending(x => x.TotalAmount).Take(8)
                    .Select(x => new BudgetEntryDto(x.TransactionDate.ToString("yyyy-MM-dd"), "Fuel", x.Station ?? "Fuel", x.Vehicle.PlateNumber, x.TotalAmount)).ToListAsync(ct));
            if (type is null || isMaint)
            {
                var wos = await _db.MaintenanceRecords.AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.CompletedAtUtc >= start && x.CompletedAtUtc < end && x.ActualCost != null)
                    .OrderByDescending(x => x.ActualCost).Take(8)
                    .Select(x => new { x.CompletedAtUtc, x.Number, x.Description, x.Vehicle.PlateNumber, x.ActualCost }).ToListAsync(ct);
                list.AddRange(wos.Select(x => new BudgetEntryDto(x.CompletedAtUtc!.Value.ToString("yyyy-MM-dd"), "Work order",
                    $"{WorkOrders.WorkOrderService.Code(x.Number)} · {x.Description}", x.PlateNumber, x.ActualCost!.Value)));
            }
            return list.OrderByDescending(x => x.Amount).Take(8).ToList();
        }

        private async Task<(Dictionary<string, string> Errors, ExpenseType? Type)> ValidateAsync(Guid organizationId, Guid? id, BudgetRequest r, CancellationToken ct)
        {
            var e = new Dictionary<string, string>();
            var name = r.Name?.Trim() ?? "";
            if (name.Length == 0) e["name"] = "Budget name is required.";
            else if (name.Length > 100) e["name"] = "Budget name must be 100 characters or fewer.";
            ExpenseType? type = null;
            var cat = r.Category?.Trim() ?? "";
            if (cat.Length == 0) e["category"] = "Choose a category.";
            else if (!string.Equals(cat, Overall, StringComparison.OrdinalIgnoreCase))
            {
                type = (await SpendQueries.ActiveTypesAsync(_db, organizationId, ct)).FirstOrDefault(t => string.Equals(t.Name, cat, StringComparison.OrdinalIgnoreCase));
                if (type is null) e["category"] = "Choose Overall or an expense type.";
            }
            if (r.LimitAmount is null) e["limitAmount"] = "Limit amount is required.";
            else if (r.LimitAmount <= 0 || r.LimitAmount > 1_000_000_000) e["limitAmount"] = "Limit must be greater than 0 and at most 1,000,000,000.";
            if (r.Year is null or < 2000 or > 2100) e["year"] = "Enter a year between 2000 and 2100.";
            if (r.Month is null or < 1 or > 12) e["month"] = "Choose a month.";
            if ((r.Notes?.Length ?? 0) > 500) e["notes"] = "Notes must be 500 characters or fewer.";
            if (e.Count == 0)
            {
                var tid = type?.Id;
                var dup = await _db.Budgets.AnyAsync(b => b.OrganizationId == organizationId && b.CurrentState == Active && b.Id != id
                    && b.Year == r.Year && b.Month == r.Month && b.ExpenseTypeId == tid
                    && (tid != null || b.Category == Overall), ct);
                if (dup) e["category"] = $"A {(type?.Name ?? Overall)} budget already exists for {new DateTime(r.Year!.Value, r.Month!.Value, 1).ToString("MMMM yyyy", Gb)}.";
            }
            return (e, type);
        }

        private static void Apply(Budget b, BudgetRequest r, ExpenseType? type)
        {
            b.Name = r.Name!.Trim();
            b.Category = type?.Name ?? Overall;
            b.ExpenseTypeId = type?.Id;
            b.Year = r.Year!.Value;
            b.Month = r.Month!.Value;
            b.LimitAmount = Math.Round(r.LimitAmount!.Value, 2);
            b.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        }

        private async Task<BudgetRowDto> RowAsync(Guid organizationId, Budget b, CancellationToken ct)
        {
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var start = new DateTime(b.Year, b.Month, 1);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, start, start.AddMonths(1), ct);
            return Row(b, types, spend);
        }

        private static ServiceResult<BudgetRowDto> Fail(Dictionary<string, string> e) =>
            e.TryGetValue("category", out var msg) && msg.Contains("already exists")
                ? ServiceResult<BudgetRowDto>.Conflict("BUDGET_EXISTS", msg, "category")
                : ServiceResult<BudgetRowDto>.Invalid(e);

        public async Task<ServiceResult<BudgetRowDto>> CreateAsync(Guid organizationId, Guid userId, BudgetRequest request, CancellationToken ct = default)
        {
            var (e, type) = await ValidateAsync(organizationId, null, request, ct);
            if (e.Count > 0) return Fail(e);
            var b = new Budget { OrganizationId = organizationId, CurrentState = Active, CreatedBy = userId, CreatedAt = DateTime.UtcNow };
            Apply(b, request, type);
            _db.Budgets.Add(b);
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetRowDto>.Ok(await RowAsync(organizationId, b, ct));
        }

        public async Task<ServiceResult<BudgetRowDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, BudgetRequest request, CancellationToken ct = default)
        {
            var b = await _db.Budgets.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (b is null) return ServiceResult<BudgetRowDto>.NotFound("Budget not found.");
            var (e, type) = await ValidateAsync(organizationId, id, request, ct);
            if (e.Count > 0) return Fail(e);
            Apply(b, request, type);
            b.UpdatedAt = DateTime.UtcNow;
            b.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetRowDto>.Ok(await RowAsync(organizationId, b, ct));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            var b = await _db.Budgets.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (b is null) return ServiceResult<bool>.NotFound("Budget not found.");
            _db.Budgets.Remove(b);
            await _db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }
    }
}
