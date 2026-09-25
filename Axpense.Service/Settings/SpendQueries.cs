using Axpense.Data.Constants;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Settings
{
    public enum SpendSource { Expense, Fuel, WorkOrder }

    public sealed record SpendRow(DateTime Date, decimal Amount, Guid? ExpenseTypeId, SpendSource Source = SpendSource.Expense);

    /// <summary>
    /// Actual spend, the same definition the dashboard uses: expenses + fuel transactions +
    /// completed maintenance cost. Each row is mapped to an expense type: expenses by their category
    /// name, fuel → the "fuel" system type, maintenance → the "maintenance" system type, and anything
    /// unmatched → the "other" system type.
    /// </summary>
    public static class SpendQueries
    {
        public static async Task<List<SpendRow>> SpendAsync(AxpenseDbContext db, Guid organizationId, IReadOnlyList<ExpenseType> types,
            DateTime? from, DateTime? to, CancellationToken ct)
        {
            var active = (int)CurrentStatusType.Active;
            var byName = types.GroupBy(t => t.Name.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Id);
            Guid? System(string key) => types.FirstOrDefault(t => t.SystemKey == key)?.Id;
            var fuelId = System(SettingsDefaults.SystemFuel);
            var maintenanceId = System(SettingsDefaults.SystemMaintenance);
            var otherId = System(SettingsDefaults.SystemOther);

            var expenses = await db.Expenses.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == active
                            && (from == null || x.ExpenseDate >= from) && (to == null || x.ExpenseDate < to))
                .Select(x => new { x.ExpenseDate, x.Amount, x.Category })
                .ToListAsync(ct);
            var fuel = await db.FuelTransactions.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == active
                            && (from == null || x.TransactionDate >= from) && (to == null || x.TransactionDate < to))
                .Select(x => new { x.TransactionDate, x.TotalAmount })
                .ToListAsync(ct);
            var maintenance = await db.MaintenanceRecords.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == active
                            && x.CompletedAtUtc != null && x.ActualCost != null
                            && (from == null || x.CompletedAtUtc >= from) && (to == null || x.CompletedAtUtc < to))
                .Select(x => new { Date = x.CompletedAtUtc!.Value, Amount = x.ActualCost!.Value })
                .ToListAsync(ct);

            var rows = new List<SpendRow>(expenses.Count + fuel.Count + maintenance.Count);
            rows.AddRange(expenses.Select(x => new SpendRow(x.ExpenseDate, x.Amount,
                byName.TryGetValue((x.Category ?? "").ToLowerInvariant(), out var id) ? id : otherId)));
            rows.AddRange(fuel.Select(x => new SpendRow(x.TransactionDate, x.TotalAmount, fuelId ?? otherId, SpendSource.Fuel)));
            rows.AddRange(maintenance.Select(x => new SpendRow(x.Date, x.Amount, maintenanceId ?? otherId, SpendSource.WorkOrder)));
            return rows;
        }

        public static Task<List<ExpenseType>> ActiveTypesAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct) =>
            db.ExpenseTypes.AsNoTracking()
                .Where(t => t.OrganizationId == organizationId && t.CurrentState == (int)CurrentStatusType.Active)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                .ToListAsync(ct);
    }
}
