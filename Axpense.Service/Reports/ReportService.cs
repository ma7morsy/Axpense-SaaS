using System.Globalization;
using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Budgets;
using Axpense.Service.Common;
using Axpense.Service.Fleet;
using Axpense.Service.Settings;
using Axpense.Service.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Reports
{
    public interface IReportService
    {
        Task<ServiceResult<FuelReportDto>> FuelAsync(Guid organizationId, int days, Guid? vehicleId, CancellationToken ct = default);
        Task<ServiceResult<BudgetReportDto>> BudgetAsync(Guid organizationId, int year, CancellationToken ct = default);
        Task<PmReportDto> PmComplianceAsync(Guid organizationId, CancellationToken ct = default);
        Task<WorkOrderReportDto> WorkOrdersAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<CostReportDto>> CostAsync(Guid organizationId, int days, CancellationToken ct = default);
        Task<IssueReportDto> IssuesAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<UptimeReportDto>> UptimeAsync(Guid organizationId, int days, CancellationToken ct = default);
        Task<ServiceResult<DriverReportDto>> DriversAsync(Guid organizationId, int days, CancellationToken ct = default);
        Task<ServiceResult<OdometerReportDto>> OdometerAsync(Guid organizationId, int days, CancellationToken ct = default);
        Task<ExpenseReportDto> ExpensesAsync(Guid organizationId, CancellationToken ct = default);
    }

    /// <summary>
    /// Reports module (read-only). Windowed reports take a rolling window of 30 / 90 / 180 / 365 days ending today.
    /// Definitions (flagged in docs/MODULE-REPORTS.md):
    /// <list type="bullet">
    /// <item>Spend = expenses + fuel transactions + actual cost of completed work orders (as on the dashboard).</item>
    /// <item>Distance in a window = highest − lowest odometer reading recorded inside it (needs two readings).</item>
    /// <item>Fuel consumption per 100 = fuel after the first fill-up ÷ distance between the first and last fill-up odometers.</item>
    /// <item>Downtime = days inside the window a work order had the vehicle in the workshop (start → completion, or → today while in progress)
    /// plus days a scheduled order is past its date; uptime = 1 − downtime ÷ window.</item>
    /// <item>Driver score = rating (70 %) + valid licence (20 %) + no expired documents (10 %).</item>
    /// <item>On-time closure = completed orders closed on or before their scheduled date.</item>
    /// </list>
    /// </summary>
    public sealed class ReportService : IReportService
    {
        private readonly AxpenseDbContext _db;
        private readonly IBudgetService _annual;
        private readonly IBudgetLimitService _limits;
        private readonly IPmTaskService _pmTasks;

        public ReportService(AxpenseDbContext db, IBudgetService annual, IBudgetLimitService limits, IPmTaskService pmTasks)
        {
            _db = db;
            _annual = annual;
            _limits = limits;
            _pmTasks = pmTasks;
        }

        private static int Active => (int)CurrentStatusType.Active;
        private static DateTime Today => DateTime.UtcNow.Date;
        private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");
        private static readonly int[] Windows = [30, 90, 180, 365];

        private static string Money(decimal v) => "EGP " + v.ToString("#,##0", CultureInfo.InvariantCulture);
        private static string Num(decimal v) => v.ToString("#,##0", CultureInfo.InvariantCulture);
        private static double Pct(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round((double)(part / whole * 100), 1);
        private static ServiceResult<T> BadWindow<T>() => ServiceResult<T>.Invalid(new Dictionary<string, string> { ["days"] = "Choose a window of 30, 90, 180 or 365 days." });
        private static string Name(Vehicle v) => $"{v.Make} {v.Model}".Trim();

        private Task<List<Vehicle>> VehiclesAsync(Guid org, CancellationToken ct) =>
            _db.Vehicles.AsNoTracking().Where(v => v.OrganizationId == org && v.CurrentState == Active).OrderBy(v => v.PlateNumber).ToListAsync(ct);

        /// <summary>Distance per vehicle inside [from, today]: max − min reading (null when fewer than two readings).</summary>
        private async Task<Dictionary<Guid, decimal?>> DistancesAsync(Guid org, DateTime from, CancellationToken ct)
        {
            var r = await _db.OdometerReadings.AsNoTracking()
                .Where(o => o.OrganizationId == org && o.CurrentState == Active && o.ReadingDate >= from)
                .GroupBy(o => o.VehicleId)
                .Select(g => new { g.Key, Max = g.Max(x => x.Value), Min = g.Min(x => x.Value), N = g.Count() })
                .ToListAsync(ct);
            return r.ToDictionary(x => x.Key, x => x.N > 1 ? x.Max - x.Min : (decimal?)null);
        }

        // =====================================================================
        public async Task<ServiceResult<FuelReportDto>> FuelAsync(Guid org, int days, Guid? vehicleId, CancellationToken ct = default)
        {
            if (!Windows.Contains(days)) return BadWindow<FuelReportDto>();
            var from = Today.AddDays(-days + 1);
            var vehicles = await VehiclesAsync(org, ct);
            var fuel = await _db.FuelTransactions.AsNoTracking()
                .Where(f => f.OrganizationId == org && f.CurrentState == Active && f.TransactionDate >= from && f.TransactionDate < Today.AddDays(1))
                .ToListAsync(ct);
            var total = fuel.Sum(f => f.TotalAmount);
            var rows = new List<FuelVehicleRow>();
            foreach (var g in fuel.GroupBy(f => f.VehicleId))
            {
                var v = vehicles.FirstOrDefault(x => x.Id == g.Key);
                if (v is null) continue;
                var unit = v.FuelType == "Electric" ? "kWh" : "L";
                var ordered = g.Where(f => f.Odometer != null).OrderBy(f => f.Odometer).ToList();
                decimal? per100 = null, perKm = null;
                if (ordered.Count >= 2)
                {
                    var dist = ordered[^1].Odometer!.Value - ordered[0].Odometer!.Value;
                    if (dist > 0)
                    {
                        per100 = Math.Round(ordered.Skip(1).Sum(f => f.QuantityLiters) / dist * 100, 1);
                        perKm = Math.Round(ordered.Skip(1).Sum(f => f.TotalAmount) / dist, 2);
                    }
                }
                rows.Add(new FuelVehicleRow(v.Id, Name(v), v.PlateNumber, v.FuelType ?? "", unit, g.Count(), g.Sum(f => f.QuantityLiters),
                    g.Sum(f => f.TotalAmount), Pct(g.Sum(f => f.TotalAmount), total), per100, perKm));
            }
            rows = rows.OrderByDescending(r => r.Cost).ToList();
            var withCons = rows.Where(r => r.PerHundred != null && r.Unit == "L").ToList();
            var worst = withCons.OrderByDescending(r => r.PerHundred).FirstOrDefault();
            var stats = new List<StatDto>
            {
                new("Fuel & energy spend", Money(total), $"{fuel.Count} fill-ups in {days} days"),
                new("Average consumption", withCons.Count > 0 ? withCons.Average(r => r.PerHundred!.Value).ToString("0.0", CultureInfo.InvariantCulture) : "—",
                    "litres per 100 km, tracked vehicles", "neutral"),
                new("Heaviest consumer", worst?.Vehicle ?? "—", worst is null ? "not enough data" : $"{worst.PerHundred:0.0} L/100km", "warn"),
                new("Vehicles without data", (vehicles.Count(v => v.Status != VehicleConstants.StatusRetired) - rows.Count).ToString(), "no fill-ups in the window"),
            };
            var logQ = fuel.Where(f => vehicleId == null || f.VehicleId == vehicleId).OrderByDescending(f => f.TransactionDate).ThenByDescending(f => f.CreatedAt).ToList();
            var log = logQ.Take(500).Select(f =>
            {
                var v = vehicles.FirstOrDefault(x => x.Id == f.VehicleId);
                return new FuelLogRow(f.Id, f.TransactionDate, f.VehicleId, v is null ? "—" : Name(v), v?.PlateNumber ?? "", f.Station, f.FuelType ?? v?.FuelType,
                    f.QuantityLiters, v?.FuelType == "Electric" ? "kWh" : "L", f.UnitPrice, f.TotalAmount, f.Odometer);
            }).ToList();
            return ServiceResult<FuelReportDto>.Ok(new FuelReportDto(days, from, Today, stats, rows, log, logQ.Count));
        }

        // =====================================================================
        public async Task<ServiceResult<BudgetReportDto>> BudgetAsync(Guid org, int year, CancellationToken ct = default)
        {
            if (year is < 2000 or > 2100) return ServiceResult<BudgetReportDto>.Invalid(new Dictionary<string, string> { ["year"] = "Enter a year between 2000 and 2100." });
            var annual = await _annual.GetAsync(org, year, ct);
            var limits = await _limits.ListAsync(org, year, null, ct);
            var now = Today;
            var plan = annual.Months.Select(m =>
            {
                var future = year > now.Year || (year == now.Year && m.Month > now.Month);
                var variance = m.Actual - m.Amount;
                return new BudgetPlanRow(m.Month, new DateTime(year, m.Month, 1).ToString("MMM", Gb), m.Amount, m.Actual, variance,
                    m.Amount > 0 ? Math.Round((double)(variance / m.Amount * 100), 1) : null, !future && m.Amount > 0 && m.Actual > m.Amount, future);
            }).ToList();
            var cats = annual.Categories.Select(c => new BudgetCategoryRow(c.Name, c.Color, c.SharePercent, c.Budget, c.Actual, c.Remaining,
                c.UtilizationPercent is null ? null : (double)c.UtilizationPercent)).ToList();
            var log = limits.Rows.OrderBy(r => r.Month).ThenBy(r => r.ExpenseTypeId == null ? 0 : 1).ThenBy(r => r.Category)
                .Select(r => new BudgetLogRow(r.Id, r.Name, r.Category, r.Period, r.Month, r.LimitAmount, r.Actual, r.Remaining, r.UtilizationPct, r.Status)).ToList();
            var ytdBudget = plan.Where(p => !p.Future).Sum(p => p.Budget);
            var stats = new List<StatDto>
            {
                new("Annual budget", annual.Exists ? Money(annual.Amount) : "Not set", annual.Exists ? $"{Money(annual.AllocatedTotal)} allocated to months" : "set it in Settings → Budget",
                    annual.Exists ? "" : "warn"),
                new("Spent this year", Money(annual.ActualTotal), ytdBudget > 0 ? $"{Pct(annual.ActualTotal, ytdBudget)}% of the year-to-date plan" : "no year-to-date plan",
                    ytdBudget > 0 && annual.ActualTotal > ytdBudget ? "bad" : ""),
                new("Months over plan", plan.Count(p => p.Over).ToString(), $"of {plan.Count(p => !p.Future)} months so far", plan.Any(p => p.Over) ? "bad" : "ok"),
                new("Monthly limits", log.Count.ToString(), $"{log.Count(l => l.Status == "Over")} over · {log.Count(l => l.Status == "At risk")} at risk", "neutral"),
            };
            return ServiceResult<BudgetReportDto>.Ok(new BudgetReportDto(year, annual.Exists, stats, plan, cats, log));
        }

        // =====================================================================
        public async Task<PmReportDto> PmComplianceAsync(Guid org, CancellationToken ct = default)
        {
            var rules = await PmRules.LoadAsync(_db, org, ct);
            var vehicles = (await VehiclesAsync(org, ct)).Where(v => v.Status != VehicleConstants.StatusRetired).ToList();
            var rows = new List<PmComplianceRow>();
            var none = 0;
            foreach (var v in vehicles)
            {
                var pm = PmCalculator.Evaluate(v, Today, rules);
                if (pm.Status == "none" || pm.Lead is null) { none++; continue; }
                rows.Add(new PmComplianceRow(v.Id, Name(v), v.PlateNumber, pm.Trigger, pm.Lead.Total, pm.Lead.Unit, pm.Percent, v.CurrentOdometer,
                    v.ReadingUnit == VehicleConstants.UnitHours ? "hr" : "km", pm.Label, v.PmLastServiceDate, pm.Status, pm.PreAlertPercent,
                    PmCalculator.DispatchBlocked(v, pm, rules)));
            }
            rows = rows.OrderByDescending(r => r.ConsumedPct).ToList();
            var overdue = rows.Count(r => r.Status == "overdue");
            var auto = await _db.MaintenanceRecords.CountAsync(m => m.OrganizationId == org && m.CurrentState == Active && m.Source == MaintenanceConstants.SourcePmEngine, ct);
            var rate = rows.Count == 0 ? 100 : (int)Math.Round((rows.Count - overdue) * 100.0 / rows.Count);
            var stats = new List<StatDto>
            {
                new("Compliance rate", $"{rate}%", $"{rows.Count - overdue} of {rows.Count} inside their interval", rate < 100 ? "bad" : "ok"),
                new("Overdue", overdue.ToString(), rules.BlockDispatchOnCriticalOverdue ? "dispatch blocked while the rule is on" : "dispatch allowed (warn only)", overdue > 0 ? "bad" : ""),
                new("Inside pre-alert", rows.Count(r => r.Status == "due").ToString(), $"{Num(rules.DistancePreAlertKm)} km / {rules.TimePreAlertDays} d ahead", "warn"),
                new("Auto-generated orders", auto.ToString(), "raised by the PM engine", "neutral"),
            };
            var tasks = (await _pmTasks.ListAsync(org, ct)).Where(t => t.IsActive).Select(t => new PmTaskRow(t.Id, t.Name, t.PartCategoryName, t.Trigger,
                string.Join(" · ", new[] { t.IntervalKm is null ? null : $"{Num(t.IntervalKm.Value)} km", t.IntervalDays is null ? null : $"{t.IntervalDays} days",
                    t.IntervalHours is null ? null : $"{Num(t.IntervalHours.Value)} hr" }.Where(x => x != null)),
                t.DurationHours, t.EstimatedCost, t.Role)).ToList();
            return new PmReportDto(stats, rows, tasks, none);
        }

        // =====================================================================
        public async Task<WorkOrderReportDto> WorkOrdersAsync(Guid org, CancellationToken ct = default)
        {
            var orders = await _db.MaintenanceRecords.AsNoTracking()
                .Where(m => m.OrganizationId == org && m.CurrentState == Active)
                .Select(m => new { m.Id, m.Number, m.Type, m.Priority, m.Status, m.DueDate, m.CompletedAtUtc, m.TechnicianName, m.EstimatedCost, m.ActualCost,
                    Tasks = m.Tasks.Count, m.Vehicle.Make, m.Vehicle.Model, m.Vehicle.PlateNumber })
                .ToListAsync(ct);
            decimal Cost(decimal? actual, decimal? est) => actual ?? est ?? 0;
            string Disp(string status, DateTime due) => status == MaintenanceConstants.StatusScheduled && due.Date < Today ? MaintenanceConstants.StatusOverdue : status;
            var done = orders.Where(o => o.Status == MaintenanceConstants.StatusCompleted).ToList();
            var onTime = done.Count(o => o.CompletedAtUtc!.Value.Date <= o.DueDate.Date);
            var open = orders.Where(o => o.Status != MaintenanceConstants.StatusCompleted).ToList();
            var onTimePct = done.Count == 0 ? 0 : (int)Math.Round(onTime * 100.0 / done.Count);
            var stats = new List<StatDto>
            {
                new("On-time closure", done.Count == 0 ? "—" : $"{onTimePct}%", $"{done.Count} closed · {done.Count - onTime} late", done.Count > 0 && onTimePct < 80 ? "bad" : ""),
                new("Open orders", open.Count.ToString(), $"{Money(open.Sum(o => o.EstimatedCost ?? 0))} committed cost", "warn"),
                new("Average job cost", done.Count == 0 ? "—" : Money(done.Average(o => o.ActualCost ?? 0)),
                    orders.Count == 0 ? "" : $"{orders.Average(o => o.Tasks):0.0} tasks per order", "neutral"),
                new("Total orders", orders.Count.ToString(), $"{Money(orders.Sum(o => Cost(o.ActualCost, o.EstimatedCost)))} lifetime"),
            };
            var techs = orders.GroupBy(o => string.IsNullOrWhiteSpace(o.TechnicianName) ? "Unassigned" : o.TechnicianName!)
                .Select(g => new TechnicianRow(g.Key, g.Count(), g.Count(o => o.Status == MaintenanceConstants.StatusCompleted),
                    g.Count(o => o.Status != MaintenanceConstants.StatusCompleted), g.Count(o => Disp(o.Status, o.DueDate) == MaintenanceConstants.StatusOverdue),
                    g.Sum(o => Cost(o.ActualCost, o.EstimatedCost))))
                .OrderByDescending(t => t.Assigned).ToList();
            var types = MaintenanceConstants.Types.Select(t => orders.Where(o => o.Type == t).ToList())
                .Select((l, i) => new OrderTypeRow(MaintenanceConstants.Types[i], l.Count, l.Sum(o => o.Tasks), Pct(l.Count, orders.Count),
                    l.Sum(o => Cost(o.ActualCost, o.EstimatedCost)))).ToList();
            var register = orders.OrderByDescending(o => o.DueDate).ThenByDescending(o => o.Number).Take(300)
                .Select(o => new OrderRegisterRow(o.Id, WorkOrderService.Code(o.Number), $"{o.Make} {o.Model}".Trim(), o.PlateNumber, o.Type, o.Priority,
                    o.TechnicianName, o.DueDate, o.Status != MaintenanceConstants.StatusCompleted && o.DueDate.Date < Today ? (int)(Today - o.DueDate.Date).TotalDays : null,
                    Cost(o.ActualCost, o.EstimatedCost), Disp(o.Status, o.DueDate))).ToList();
            return new WorkOrderReportDto(stats, techs, types, register);
        }

        // =====================================================================
        public async Task<ServiceResult<CostReportDto>> CostAsync(Guid org, int days, CancellationToken ct = default)
        {
            if (!Windows.Contains(days)) return BadWindow<CostReportDto>();
            var from = Today.AddDays(-days + 1);
            var to = Today.AddDays(1);
            var vehicles = await VehiclesAsync(org, ct);
            var types = await SpendQueries.ActiveTypesAsync(_db, org, ct);
            var spend = await SpendQueries.SpendAsync(_db, org, types, from.AddDays(-days), to, ct);
            var cur = spend.Where(s => s.Date >= from).ToList();
            var prev = spend.Where(s => s.Date < from).Sum(s => s.Amount);

            var exp = await _db.Expenses.AsNoTracking().Where(x => x.OrganizationId == org && x.CurrentState == Active && x.ExpenseDate >= from && x.ExpenseDate < to && x.VehicleId != null)
                .GroupBy(x => x.VehicleId!.Value).Select(g => new { g.Key, S = g.Sum(x => x.Amount) }).ToListAsync(ct);
            var fuel = await _db.FuelTransactions.AsNoTracking().Where(x => x.OrganizationId == org && x.CurrentState == Active && x.TransactionDate >= from && x.TransactionDate < to)
                .GroupBy(x => x.VehicleId).Select(g => new { g.Key, S = g.Sum(x => x.TotalAmount) }).ToListAsync(ct);
            var wos = await _db.MaintenanceRecords.AsNoTracking().Where(x => x.OrganizationId == org && x.CurrentState == Active && x.CompletedAtUtc >= from && x.CompletedAtUtc < to && x.ActualCost != null)
                .GroupBy(x => x.VehicleId).Select(g => new { g.Key, S = g.Sum(x => x.ActualCost!.Value) }).ToListAsync(ct);
            var dist = await DistancesAsync(org, from, ct);
            var rows = vehicles.Select(v =>
            {
                var e = exp.FirstOrDefault(x => x.Key == v.Id)?.S ?? 0;
                var f = fuel.FirstOrDefault(x => x.Key == v.Id)?.S ?? 0;
                var w = wos.FirstOrDefault(x => x.Key == v.Id)?.S ?? 0;
                var d = dist.GetValueOrDefault(v.Id);
                return new CostVehicleRow(v.Id, Name(v), v.PlateNumber, e, f, w, e + f + w, 0, d, d is > 0 ? Math.Round((e + f + w) / d.Value, 2) : null);
            }).ToList();
            var vehTotal = rows.Sum(r => r.Total);
            rows = rows.Select(r => r with { SharePct = Pct(r.Total, vehTotal) }).OrderByDescending(r => r.Total).ToList();

            var annual = await _annual.GetAsync(org, Today.Year, ct);
            var total = cur.Sum(s => s.Amount);
            var cats = types.Select(t =>
            {
                var list = cur.Where(s => s.ExpenseTypeId == t.Id).ToList();
                var a = list.Sum(s => s.Amount);
                var share = annual.Categories.FirstOrDefault(c => c.ExpenseTypeId == t.Id);
                // The annual category budget prorated to the window.
                decimal? budget = share is null || share.Budget == 0 ? null : Math.Round(share.Budget * days / 365m, 2);
                return new CostCategoryRow(t.Name, t.Color, list.Count, a, Pct(a, total), budget, budget is > 0 ? Math.Round((double)((a - budget.Value) / budget.Value * 100), 1) : null);
            }).OrderByDescending(c => c.Total).ToList();
            var top = rows.FirstOrDefault(r => r.Total > 0);
            var committed = await _db.MaintenanceRecords.Where(m => m.OrganizationId == org && m.CurrentState == Active && m.Status != MaintenanceConstants.StatusCompleted)
                .SumAsync(m => (decimal?)m.EstimatedCost, ct) ?? 0;
            var stats = new List<StatDto>
            {
                new($"Spend, last {days} days", Money(total), prev > 0 ? $"previous {days} days: {Money(prev)}" : "no spend in the previous window"),
                new("Maintenance committed", Money(committed), "estimated cost of open work orders", "neutral"),
                new("Most expensive vehicle", top?.Vehicle ?? "—", top is null ? "" : $"{Money(top.Total)} in {days} days", "warn"),
                new("Change vs previous", prev > 0 ? $"{(total >= prev ? "+" : "")}{Pct(total - prev, prev)}%" : "—", $"against the {days} days before", total > prev && prev > 0 ? "bad" : "ok"),
            };
            return ServiceResult<CostReportDto>.Ok(new CostReportDto(days, stats, rows, cats));
        }

        // =====================================================================
        public async Task<IssueReportDto> IssuesAsync(Guid org, CancellationToken ct = default)
        {
            var issues = await _db.VehicleIssues.AsNoTracking().Where(i => i.OrganizationId == org && i.CurrentState == Active)
                .Select(i => new { i.Id, i.VehicleId, i.Vehicle.Make, i.Vehicle.Model, i.Vehicle.PlateNumber, i.Status, i.Priority, i.Source, i.ReportedDate,
                    Part = i.PresetPart != null ? i.PresetPart.Name : null })
                .ToListAsync(ct);
            var ids = issues.Select(i => (Guid?)i.Id).ToList();
            var costs = await _db.MaintenanceRecords.AsNoTracking().Where(m => m.OrganizationId == org && m.CurrentState == Active && ids.Contains(m.IssueId))
                .Select(m => new { m.IssueId, C = m.ActualCost ?? m.EstimatedCost ?? 0 }).ToListAsync(ct);
            var open = issues.Where(i => i.Status != VehicleConstants.IssueResolved).ToList();
            var parts = issues.GroupBy(i => i.Part ?? "Unclassified").Select(g => new IssuePartRow(g.Key, g.Count(), g.Select(x => x.VehicleId).Distinct().Count(),
                    g.Count(x => x.Status != VehicleConstants.IssueResolved), g.Max(x => x.ReportedDate),
                    costs.Where(c => g.Any(x => x.Id == c.IssueId)).Sum(c => c.C), g.Count() > 1))
                .OrderByDescending(p => p.Occurrences).ThenByDescending(p => p.Open).ToList();
            var byVeh = issues.GroupBy(i => i.VehicleId).Select(g => new IssueVehicleRow(g.Key, $"{g.First().Make} {g.First().Model}".Trim(), g.First().PlateNumber,
                g.Count(), g.Count(x => x.Status != VehicleConstants.IssueResolved))).OrderByDescending(x => x.Issues).ToList();
            var bySrc = issues.GroupBy(i => i.Source.StartsWith("Inspection") ? "Inspection" : i.Source)
                .Select(g => new IssueSourceRow(g.Key, g.Count(), Pct(g.Count(), issues.Count))).OrderByDescending(x => x.Issues).ToList();
            var stats = new List<StatDto>
            {
                new("Total issues", issues.Count.ToString(), $"{issues.Count - open.Count} resolved"),
                new("Open", open.Count.ToString(), $"{open.Count(i => i.Priority == VehicleConstants.PriorityHigh)} high priority", open.Count > 0 ? "bad" : "ok"),
                new("Recurring parts", parts.Count(p => p.Recurring && p.Part != "Unclassified").ToString(), "seen more than once", "warn"),
                new("From inspections", issues.Count(i => i.Source.StartsWith("Inspection")).ToString(), "raised automatically on failure", "neutral"),
            };
            return new IssueReportDto(stats, parts, byVeh, bySrc);
        }

        // =====================================================================
        public async Task<ServiceResult<UptimeReportDto>> UptimeAsync(Guid org, int days, CancellationToken ct = default)
        {
            if (!Windows.Contains(days)) return BadWindow<UptimeReportDto>();
            var from = Today.AddDays(-days + 1);
            var vehicles = (await VehiclesAsync(org, ct)).Where(v => v.Status != VehicleConstants.StatusRetired).ToList();
            var orders = await _db.MaintenanceRecords.AsNoTracking().Where(m => m.OrganizationId == org && m.CurrentState == Active)
                .Select(m => new { m.VehicleId, m.Status, m.DueDate, m.StartedAtUtc, m.CompletedAtUtc, m.CreatedAt }).ToListAsync(ct);
            var dist = await DistancesAsync(org, from, ct);
            int Overlap(DateTime s, DateTime e)
            {
                var a = s.Date < from ? from : s.Date;
                var b = e.Date > Today ? Today : e.Date;
                return b < a ? 0 : (int)(b - a).TotalDays + 1;
            }
            var rows = vehicles.Select(v =>
            {
                var mine = orders.Where(o => o.VehicleId == v.Id).ToList();
                var down = new HashSet<DateTime>();
                void Add(DateTime s, DateTime e) { for (var d = (s.Date < from ? from : s.Date); d <= (e.Date > Today ? Today : e.Date); d = d.AddDays(1)) down.Add(d); }
                foreach (var o in mine)
                {
                    if (o.Status == MaintenanceConstants.StatusCompleted && o.CompletedAtUtc is { } c) Add(o.StartedAtUtc ?? c, c);
                    else if (o.Status == MaintenanceConstants.StatusInProgress) Add(o.StartedAtUtc ?? o.DueDate, Today);
                    else if (o.Status == MaintenanceConstants.StatusScheduled && o.DueDate.Date < Today) Add(o.DueDate.AddDays(1), Today);
                }
                var inWindow = mine.Count(o => Overlap(o.StartedAtUtc ?? o.DueDate, o.CompletedAtUtc ?? Today) > 0 || o.DueDate >= from);
                var up = Math.Round(100 - down.Count * 100.0 / days, 1);
                return new UptimeRow(v.Id, Name(v), v.PlateNumber, v.Status, inWindow, down.Count, dist.GetValueOrDefault(v.Id), up);
            }).OrderBy(r => r.UptimePct).ToList();
            var best = rows.LastOrDefault();
            var fleet = rows.Count == 0 ? 100 : Math.Round(rows.Average(r => r.UptimePct), 1);
            var stats = new List<StatDto>
            {
                new("Fleet uptime", $"{fleet}%", $"rolling {days} days", fleet < 90 ? "warn" : ""),
                new("Downtime days", rows.Sum(r => r.DowntimeDays).ToString(), $"across {rows.Count} vehicles", "bad"),
                new("Off the road now", vehicles.Count(v => v.Status is VehicleConstants.StatusUnderMaintenance or VehicleConstants.StatusDown).ToString(),
                    "in the workshop or grounded", "neutral"),
                new("Best performer", best?.Vehicle ?? "—", best is null ? "" : $"{best.UptimePct}% uptime"),
            };
            return ServiceResult<UptimeReportDto>.Ok(new UptimeReportDto(days, stats, rows));
        }

        // =====================================================================
        public async Task<ServiceResult<DriverReportDto>> DriversAsync(Guid org, int days, CancellationToken ct = default)
        {
            if (!Windows.Contains(days)) return BadWindow<DriverReportDto>();
            var from = Today.AddDays(-days + 1);
            var drivers = await _db.Drivers.AsNoTracking().Where(d => d.OrganizationId == org && d.CurrentState == Active).OrderBy(d => d.FullName).ToListAsync(ct);
            var docs = await _db.DriverDocuments.AsNoTracking().Where(d => d.OrganizationId == org && d.CurrentState == Active && d.ExpiryDate < Today)
                .GroupBy(d => d.DriverId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
            var assigns = await _db.VehicleAssignments.AsNoTracking().Where(a => a.OrganizationId == org && a.CurrentState == Active)
                .Select(a => new { a.DriverId, a.VehicleId, a.StartDate, a.EndDate, a.Vehicle.Make, a.Vehicle.Model, a.Vehicle.PlateNumber }).ToListAsync(ct);
            var types = await SpendQueries.ActiveTypesAsync(_db, org, ct);
            var vSpend = new Dictionary<Guid, decimal>();
            foreach (var s in await _db.Expenses.AsNoTracking().Where(x => x.OrganizationId == org && x.CurrentState == Active && x.ExpenseDate >= from && x.VehicleId != null)
                         .GroupBy(x => x.VehicleId!.Value).Select(g => new { g.Key, S = g.Sum(x => x.Amount) }).ToListAsync(ct)) vSpend[s.Key] = s.S;
            foreach (var s in await _db.FuelTransactions.AsNoTracking().Where(x => x.OrganizationId == org && x.CurrentState == Active && x.TransactionDate >= from)
                         .GroupBy(x => x.VehicleId).Select(g => new { g.Key, S = g.Sum(x => x.TotalAmount) }).ToListAsync(ct)) vSpend[s.Key] = vSpend.GetValueOrDefault(s.Key) + s.S;
            var rows = drivers.Select(d =>
            {
                var current = assigns.Where(a => a.DriverId == d.Id && a.StartDate < Today.AddDays(1) && (a.EndDate == null || a.EndDate > Today)).OrderByDescending(a => a.StartDate).FirstOrDefault();
                int? licDays = d.LicenseExpiryDate is null ? null : (int)(d.LicenseExpiryDate.Value.Date - Today).TotalDays;
                var expired = docs.FirstOrDefault(x => x.Key == d.Id)?.N ?? 0;
                var score = (int)Math.Round((double)(d.Rating / 5m * 70) + (licDays is >= 0 ? 20 : 0) + (expired == 0 ? 10 : 0));
                return new DriverRow(d.Id, d.FullName, d.LicenseClass, d.Status, current is null ? null : $"{current.Make} {current.Model}".Trim(), current?.PlateNumber,
                    d.Rating, licDays, expired, assigns.Count(a => a.DriverId == d.Id), current is null ? 0 : vSpend.GetValueOrDefault(current.VehicleId), Math.Clamp(score, 0, 100));
            }).OrderByDescending(r => r.Score).ToList();
            var nonCompliant = rows.Count(r => r.LicenseDays is null or < 0 || r.ExpiredDocuments > 0);
            var stats = new List<StatDto>
            {
                new("Average score", rows.Count == 0 ? "—" : Math.Round(rows.Average(r => r.Score)).ToString(CultureInfo.InvariantCulture), $"across {rows.Count} drivers"),
                new("Document compliance", $"{rows.Count - nonCompliant}/{rows.Count}", $"{nonCompliant} with an expired or missing licence or document", nonCompliant > 0 ? "bad" : "ok"),
                new("Licences expiring", rows.Count(r => r.LicenseDays is >= 0 and <= 30).ToString(), "within 30 days", "warn"),
                new("Unassigned drivers", rows.Count(r => r.Vehicle is null).ToString(), "no vehicle today", "neutral"),
            };
            return ServiceResult<DriverReportDto>.Ok(new DriverReportDto(days, stats, rows));
        }

        // =====================================================================
        public async Task<ServiceResult<OdometerReportDto>> OdometerAsync(Guid org, int days, CancellationToken ct = default)
        {
            if (!Windows.Contains(days)) return BadWindow<OdometerReportDto>();
            var from = Today.AddDays(-days + 1);
            var vehicles = (await VehiclesAsync(org, ct)).Where(v => v.Status != VehicleConstants.StatusRetired).ToList();
            var readings = await _db.OdometerReadings.AsNoTracking().Where(o => o.OrganizationId == org && o.CurrentState == Active)
                .Select(o => new { o.VehicleId, o.ReadingDate, o.Value, o.Source }).ToListAsync(ct);
            var rows = vehicles.Select(v =>
            {
                var rs = readings.Where(r => r.VehicleId == v.Id).OrderBy(r => r.ReadingDate).ThenBy(r => r.Value).ToList();
                var win = rs.Where(r => r.ReadingDate >= from).ToList();
                decimal? dist = win.Count > 1 ? win.Max(r => r.Value) - win.Min(r => r.Value) : null;
                var span = win.Count > 1 ? Math.Max(1, (win[^1].ReadingDate.Date - win[0].ReadingDate.Date).Days) : 0;
                var last = rs.LastOrDefault();
                return new OdometerRow(v.Id, Name(v), v.PlateNumber, v.CurrentOdometer, v.ReadingUnit == VehicleConstants.UnitHours ? "hr" : "km", rs.Count, dist,
                    dist is not null && span > 0 ? Math.Round(dist.Value / span, 1) : null, last?.ReadingDate, last?.Source,
                    last is null || (Today - last.ReadingDate.Date).TotalDays > 14);
            }).OrderByDescending(r => r.Distance ?? -1).ToList();
            var top = vehicles.OrderByDescending(v => v.CurrentOdometer ?? 0).FirstOrDefault();
            var stats = new List<StatDto>
            {
                new($"Distance logged, {days} days", Num(rows.Sum(r => r.Distance ?? 0)), "kilometres and engine hours combined"),
                new("Readings on file", readings.Count.ToString(), "manual, fuel, inspection and work order sources", "neutral"),
                new("Stale readings", rows.Count(r => r.Stale).ToString(), "no reading in the last 14 days", rows.Any(r => r.Stale) ? "warn" : "ok"),
                new("Highest reading", top is null ? "—" : Num(top.CurrentOdometer ?? 0), top is null ? "" : Name(top)),
            };
            return ServiceResult<OdometerReportDto>.Ok(new OdometerReportDto(days, stats, rows));
        }

        // =====================================================================
        public async Task<ExpenseReportDto> ExpensesAsync(Guid org, CancellationToken ct = default)
        {
            await OrganizationDefaultsSeeder.SeedExpenseTypesAsync(_db, org, ct);
            var types = await SpendQueries.ActiveTypesAsync(_db, org, ct);
            var m0 = new DateTime(Today.Year, Today.Month, 1);
            var all = await SpendQueries.SpendAsync(_db, org, types, null, Today.AddDays(1), ct);
            var months = Enumerable.Range(0, 7).Select(i => m0.AddMonths(i - 6)).Select(s => new ForecastMonth(s.ToString("yyyy-MM"), s.ToString("MMM", Gb),
                all.Where(r => r.Date >= s && r.Date < s.AddMonths(1)).Sum(r => r.Amount), s == m0)).ToList();
            var mtd = months[^1].Total;
            var dim = DateTime.DaysInMonth(Today.Year, Today.Month);
            var projection = Math.Round(mtd / Today.Day * dim, 0);
            var base3 = months.Skip(3).Take(3).Average(m => m.Total);
            var last = months[^2];
            var byType = types.Select(t =>
            {
                var l = all.Where(r => r.ExpenseTypeId == t.Id).ToList();
                var l30 = l.Where(r => r.Date >= Today.AddDays(-29)).Sum(r => r.Amount);
                var p30 = l.Where(r => r.Date >= Today.AddDays(-59) && r.Date < Today.AddDays(-29)).Sum(r => r.Amount);
                var trend = p30 > 0 ? (int)Math.Round((l30 - p30) / p30 * 100) : l30 > 0 ? 100 : 0;
                return new ExpenseTypeTrendRow(t.Name, t.Color, l.Count, l.Sum(r => r.Amount), l30, p30, trend);
            }).OrderByDescending(r => r.Lifetime).ToList();
            var stats = new List<StatDto>
            {
                new("Month to date", Money(mtd), $"day {Today.Day} of {dim}"),
                new("Run-rate projection", Money(projection), "this month at the current pace", "neutral"),
                new("3-month average", Money(base3), projection > base3 ? $"projecting {Pct(projection - base3, base3)}% above" : "projecting inside the average",
                    projection > base3 * 1.15m ? "bad" : ""),
                new("Last full month", Money(last.Total), last.Label),
            };
            return new ExpenseReportDto(stats, months, projection, byType);
        }
    }
}
