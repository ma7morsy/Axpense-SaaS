using Axpense.Data.Entities;
using Axpense.Data.Entities.SaasEntities;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Common;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Settings
{
    public interface IBudgetService
    {
        Task<BudgetDto> GetAsync(Guid organizationId, int year, CancellationToken ct = default);
        Task<ServiceResult<BudgetDto>> SetAnnualAsync(Guid organizationId, Guid userId, int year, decimal amount, CancellationToken ct = default);
        Task<ServiceResult<BudgetDto>> SetMonthAsync(Guid organizationId, Guid userId, int year, int month, decimal amount, CancellationToken ct = default);
        Task<ServiceResult<BudgetDto>> SplitEvenlyAsync(Guid organizationId, Guid userId, int year, CancellationToken ct = default);
        Task<ServiceResult<BudgetDto>> WeightByLastYearAsync(Guid organizationId, Guid userId, int year, CancellationToken ct = default);
        Task<ServiceResult<BudgetDto>> SetShareAsync(Guid organizationId, Guid userId, int year, Guid expenseTypeId, decimal sharePercent, CancellationToken ct = default);
        Task<ServiceResult<BudgetDto>> DistributeFromLastYearAsync(Guid organizationId, Guid userId, int year, CancellationToken ct = default);
    }

    /// <summary>
    /// Annual budget per calendar year.
    /// - Setting the annual amount (re)splits the months evenly; overriding a month leaves the others
    ///   as they are, so the plan can drift from the annual figure — the drift is reported, not hidden.
    /// - Category shares are percentages of the annual amount per expense type and should total 100%.
    ///   New budgets start from last year's actual mix (or an even split when there's no history).
    /// - Actuals use the dashboard's spend definition (expenses + fuel + completed maintenance).
    /// </summary>
    public sealed class BudgetService : IBudgetService
    {
        private const int MinYear = 2000, MaxYear = 2100;
        private const decimal MaxAmount = 1_000_000_000_000m;
        private readonly AxpenseDbContext _db;

        public BudgetService(AxpenseDbContext db) => _db = db;

        public async Task<BudgetDto> GetAsync(Guid organizationId, int year, CancellationToken ct = default)
        {
            await OrganizationDefaultsSeeder.SeedExpenseTypesAsync(_db, organizationId, ct);
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var budget = await Load(organizationId, year).AsNoTracking().FirstOrDefaultAsync(ct);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1), ct);
            var prior = await SpendQueries.SpendAsync(_db, organizationId, types, new DateTime(year - 1, 1, 1), new DateTime(year, 1, 1), ct);

            var amount = budget?.Amount ?? 0;
            var actualTotal = spend.Sum(r => r.Amount);
            var priorTotal = prior.Sum(r => r.Amount);
            var suggested = Math.Round(((priorTotal > 0 ? priorTotal : actualTotal) * 1.05m) / 10_000m) * 10_000m;

            var monthAmounts = Enumerable.Range(1, 12)
                .Select(m => budget?.Months.FirstOrDefault(x => x.Month == m)?.Amount ?? (amount > 0 ? Math.Round(amount / 12, 2) : 0))
                .ToArray();
            var months = Enumerable.Range(1, 12).Select(m =>
            {
                var alloc = monthAmounts[m - 1];
                var actual = spend.Where(r => r.Date.Month == m).Sum(r => r.Amount);
                return new BudgetMonthDto(m, alloc, Pct(alloc, amount), actual, alloc > 0 ? Math.Round(actual / alloc * 100, 1) : null);
            }).ToList();

            var shares = budget?.CategoryShares.ToDictionary(s => s.ExpenseTypeId, s => s.SharePercent) ?? [];
            var categories = types.Select(t =>
            {
                var share = shares.GetValueOrDefault(t.Id);
                var catBudget = Math.Round(amount * share / 100, 2);
                var actual = spend.Where(r => r.ExpenseTypeId == t.Id).Sum(r => r.Amount);
                return new BudgetCategoryDto(t.Id, t.Name, t.Color, share, catBudget, actual, catBudget - actual,
                    catBudget > 0 ? Math.Round(actual / catBudget * 100, 1) : null);
            }).ToList();

            var allocated = monthAmounts.Sum();
            var now = DateTime.UtcNow;
            return new BudgetDto(year, budget is not null, amount, suggested, allocated, amount - allocated, actualTotal,
                categories.Sum(c => c.SharePercent), now.Year == year ? now.Month : null, priorTotal > 0, months, categories);
        }

        public async Task<ServiceResult<BudgetDto>> SetAnnualAsync(Guid organizationId, Guid userId, int year, decimal amount, CancellationToken ct = default)
        {
            var invalid = ValidateYear(year) ?? ValidateAmount(amount, "amount", "Annual budget");
            if (invalid is not null) return invalid;

            var budget = await Load(organizationId, year).FirstOrDefaultAsync(ct);
            var isNew = budget is null;
            if (budget is null)
            {
                budget = new AnnualBudget { OrganizationId = organizationId, Year = year, CreatedBy = userId, CurrentState = (int)CurrentStatusType.Active };
                _db.AnnualBudgets.Add(budget);
            }
            budget.Amount = Math.Round(amount, 2);
            Touch(budget, userId);
            SpreadEvenly(budget, organizationId, userId);

            if (isNew)
            {
                var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
                var mix = await LastYearMixAsync(organizationId, year, types, ct) ?? EvenMix(types);
                foreach (var (typeId, pct) in mix)
                    AddShare(budget, organizationId, userId, typeId).SharePercent = pct;
            }
            Audit(organizationId, userId, budget, $"Annual budget {year} set to {budget.Amount:N0}");
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetDto>.Ok(await GetAsync(organizationId, year, ct));
        }

        public async Task<ServiceResult<BudgetDto>> SetMonthAsync(Guid organizationId, Guid userId, int year, int month, decimal amount, CancellationToken ct = default)
        {
            if (month is < 1 or > 12) return Invalid("month", "Month must be between 1 and 12.");
            var invalid = ValidateAmount(amount, "amount", "Monthly allocation");
            if (invalid is not null) return invalid;

            var budget = await Load(organizationId, year).FirstOrDefaultAsync(ct);
            if (budget is null) return NoBudget(year);
            var row = budget.Months.FirstOrDefault(m => m.Month == month);
            if (row is null)
            {
                SpreadEvenly(budget, organizationId, userId);
                row = budget.Months.First(m => m.Month == month);
            }
            row.Amount = Math.Round(amount, 2);
            Touch(row, userId);
            Audit(organizationId, userId, budget, $"Month {month}/{year} allocation set to {row.Amount:N0}");
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetDto>.Ok(await GetAsync(organizationId, year, ct));
        }

        public async Task<ServiceResult<BudgetDto>> SplitEvenlyAsync(Guid organizationId, Guid userId, int year, CancellationToken ct = default)
        {
            var budget = await Load(organizationId, year).FirstOrDefaultAsync(ct);
            if (budget is null) return NoBudget(year);
            SpreadEvenly(budget, organizationId, userId);
            Audit(organizationId, userId, budget, $"Monthly plan {year} split evenly");
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetDto>.Ok(await GetAsync(organizationId, year, ct));
        }

        public async Task<ServiceResult<BudgetDto>> WeightByLastYearAsync(Guid organizationId, Guid userId, int year, CancellationToken ct = default)
        {
            var budget = await Load(organizationId, year).FirstOrDefaultAsync(ct);
            if (budget is null) return NoBudget(year);

            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var prior = await SpendQueries.SpendAsync(_db, organizationId, types, new DateTime(year - 1, 1, 1), new DateTime(year, 1, 1), ct);
            var total = prior.Sum(r => r.Amount);
            if (total <= 0) return Invalid("months", $"There is no spend recorded in {year - 1} to weight the months by.");

            var weights = Enumerable.Range(1, 12).Select(m => prior.Where(r => r.Date.Month == m).Sum(r => r.Amount) / total).ToArray();
            var amounts = Distribute(budget.Amount, weights);
            EnsureMonths(budget, organizationId, userId);
            foreach (var row in budget.Months) { row.Amount = amounts[row.Month - 1]; Touch(row, userId); }
            Audit(organizationId, userId, budget, $"Monthly plan {year} weighted by {year - 1} spend");
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetDto>.Ok(await GetAsync(organizationId, year, ct));
        }

        public async Task<ServiceResult<BudgetDto>> SetShareAsync(Guid organizationId, Guid userId, int year, Guid expenseTypeId, decimal sharePercent, CancellationToken ct = default)
        {
            if (sharePercent is < 0 or > 100) return Invalid("sharePercent", "Share must be between 0 and 100%.");
            var budget = await Load(organizationId, year).FirstOrDefaultAsync(ct);
            if (budget is null) return NoBudget(year);
            var typeExists = await _db.ExpenseTypes.AnyAsync(t => t.Id == expenseTypeId && t.OrganizationId == organizationId && t.CurrentState == (int)CurrentStatusType.Active, ct);
            if (!typeExists) return ServiceResult<BudgetDto>.NotFound("Expense type not found.");

            var share = budget.CategoryShares.FirstOrDefault(s => s.ExpenseTypeId == expenseTypeId)
                        ?? AddShare(budget, organizationId, userId, expenseTypeId);
            share.SharePercent = Math.Round(sharePercent, 1);
            Touch(share, userId);
            Audit(organizationId, userId, budget, $"Category share {year} set to {share.SharePercent}%");
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetDto>.Ok(await GetAsync(organizationId, year, ct));
        }

        public async Task<ServiceResult<BudgetDto>> DistributeFromLastYearAsync(Guid organizationId, Guid userId, int year, CancellationToken ct = default)
        {
            var budget = await Load(organizationId, year).FirstOrDefaultAsync(ct);
            if (budget is null) return NoBudget(year);
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var mix = await LastYearMixAsync(organizationId, year, types, ct);
            if (mix is null) return Invalid("categories", $"There is no spend recorded in {year - 1} to distribute from.");

            foreach (var (typeId, pct) in mix)
            {
                var share = budget.CategoryShares.FirstOrDefault(s => s.ExpenseTypeId == typeId)
                            ?? AddShare(budget, organizationId, userId, typeId);
                share.SharePercent = pct;
                Touch(share, userId);
            }
            Audit(organizationId, userId, budget, $"Category shares {year} distributed from {year - 1}");
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BudgetDto>.Ok(await GetAsync(organizationId, year, ct));
        }

        // ---------------------------------------------------------------- helpers

        private IQueryable<AnnualBudget> Load(Guid organizationId, int year) =>
            _db.AnnualBudgets.Include(b => b.Months).Include(b => b.CategoryShares)
                .Where(b => b.OrganizationId == organizationId && b.Year == year && b.CurrentState == (int)CurrentStatusType.Active);

        /// <summary>Last year's spend mix per type, one decimal, nudged so it totals exactly 100.</summary>
        private async Task<List<(Guid TypeId, decimal Pct)>?> LastYearMixAsync(Guid organizationId, int year, List<ExpenseType> types, CancellationToken ct)
        {
            var prior = await SpendQueries.SpendAsync(_db, organizationId, types, new DateTime(year - 1, 1, 1), new DateTime(year, 1, 1), ct);
            var total = prior.Sum(r => r.Amount);
            if (total <= 0 || types.Count == 0) return null;
            var mix = types.Select(t => (t.Id, Math.Round(prior.Where(r => r.ExpenseTypeId == t.Id).Sum(r => r.Amount) / total * 100, 1))).ToList();
            return Nudge(mix);
        }

        private static List<(Guid TypeId, decimal Pct)> EvenMix(List<ExpenseType> types) =>
            types.Count == 0 ? [] : Nudge(types.Select(t => (t.Id, Math.Round(100m / types.Count, 1))).ToList());

        private static List<(Guid TypeId, decimal Pct)> Nudge(List<(Guid TypeId, decimal Pct)> mix)
        {
            var drift = 100m - mix.Sum(m => m.Pct);
            if (drift == 0 || mix.Count == 0) return mix;
            var i = mix.IndexOf(mix.OrderByDescending(m => m.Pct).First());
            mix[i] = (mix[i].TypeId, mix[i].Pct + drift);
            return mix;
        }

        private void SpreadEvenly(AnnualBudget budget, Guid organizationId, Guid userId)
        {
            EnsureMonths(budget, organizationId, userId);
            var amounts = Distribute(budget.Amount, Enumerable.Repeat(1m / 12, 12).ToArray());
            foreach (var row in budget.Months) { row.Amount = amounts[row.Month - 1]; Touch(row, userId); }
        }

        // Child rows are added through the DbSet as well: ids are generated client-side, so a row only
        // attached to a tracked parent's collection would be treated as an existing row to update.
        private void EnsureMonths(AnnualBudget budget, Guid organizationId, Guid userId)
        {
            for (var m = 1; m <= 12; m++)
                if (budget.Months.All(x => x.Month != m))
                {
                    var row = new BudgetMonthAllocation
                    {
                        OrganizationId = organizationId, AnnualBudgetId = budget.Id, Month = m,
                        CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = (int)CurrentStatusType.Active
                    };
                    budget.Months.Add(row);
                    _db.BudgetMonthAllocations.Add(row);
                }
        }

        private BudgetCategoryShare AddShare(AnnualBudget budget, Guid organizationId, Guid userId, Guid expenseTypeId)
        {
            var share = new BudgetCategoryShare
            {
                OrganizationId = organizationId, AnnualBudgetId = budget.Id, ExpenseTypeId = expenseTypeId,
                CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = (int)CurrentStatusType.Active
            };
            budget.CategoryShares.Add(share);
            _db.BudgetCategoryShares.Add(share);
            return share;
        }

        /// <summary>Splits an amount by weights, rounded to 2 decimals, the last month absorbing the rounding.</summary>
        private static decimal[] Distribute(decimal amount, decimal[] weights)
        {
            var result = weights.Select(w => Math.Round(amount * w, 2)).ToArray();
            result[^1] += amount - result.Sum();
            return result;
        }

        private static decimal Pct(decimal part, decimal whole) => whole > 0 ? Math.Round(part / whole * 100, 1) : 0;

        private static void Touch(BaseEntity e, Guid userId)
        {
            e.UpdatedBy = userId;
            e.UpdatedAt = DateTime.UtcNow;
        }

        private void Audit(Guid organizationId, Guid userId, AnnualBudget budget, string details) =>
            _db.AuditLogs.Add(new AuditLog
            {
                OrganizationId = organizationId, UserId = userId, Action = "Updated", EntityType = nameof(AnnualBudget),
                EntityId = budget.Id.ToString(), Details = details, CreatedAt = DateTime.UtcNow
            });

        private static ServiceResult<BudgetDto>? ValidateYear(int year) =>
            year is < MinYear or > MaxYear ? Invalid("year", $"Year must be between {MinYear} and {MaxYear}.") : null;

        private static ServiceResult<BudgetDto>? ValidateAmount(decimal amount, string field, string label) =>
            amount < 0 || amount > MaxAmount ? Invalid(field, $"{label} must be zero or more.") : null;

        private static ServiceResult<BudgetDto> Invalid(string field, string message) =>
            ServiceResult<BudgetDto>.Invalid(new Dictionary<string, string> { [field] = message });

        private static ServiceResult<BudgetDto> NoBudget(int year) =>
            ServiceResult<BudgetDto>.NotFound($"No budget has been set for {year} yet.");
    }
}
