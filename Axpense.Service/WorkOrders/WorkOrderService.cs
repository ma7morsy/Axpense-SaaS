using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Data.Entities.MaintenanceRecord;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Entities.VehicleEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Service.Common;
using Axpense.Service.Fleet;
using Axpense.Service.Settings;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.WorkOrders
{
    public interface IWorkOrderService
    {
        Task<WorkOrderOptionsDto> OptionsAsync(Guid organizationId, CancellationToken ct = default);
        Task<WorkOrderListResponse> ListAsync(Guid organizationId, WorkOrderListQuery query, CancellationToken ct = default);
        Task<ServiceResult<WorkOrderDetailDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);
        Task<ServiceResult<WorkOrderDetailDto>> CreateAsync(Guid organizationId, Guid userId, string userName, WorkOrderRequest request, CancellationToken ct = default);
        Task<ServiceResult<WorkOrderDetailDto>> UpdateAsync(Guid organizationId, Guid userId, string userName, Guid id, WorkOrderRequest request, CancellationToken ct = default);
        Task<ServiceResult<WorkOrderDetailDto>> SetStatusAsync(Guid organizationId, Guid userId, string userName, Guid id, WorkOrderStatusRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);
        Task<PmEnginePreviewDto> PreviewPmEngineAsync(Guid organizationId, CancellationToken ct = default);
        Task<PmEngineRunResult> RunPmEngineAsync(Guid organizationId, Guid userId, IReadOnlyCollection<Guid>? vehicleIds = null, CancellationToken ct = default);
        Task<List<FleetIssueDto>> IssuesAsync(Guid organizationId, string? status, CancellationToken ct = default);
    }

    /// <summary>
    /// Work orders (maintenance module). Rules:
    /// <list type="bullet">
    /// <item>A work order belongs to one active, non-retired vehicle and has 1–40 tasks; its estimated total is the sum of task costs.</item>
    /// <item>Statuses: Scheduled → In progress → Completed. "Overdue" is derived (still Scheduled after its date) and escalates per the PM engine chain.</item>
    /// <item>Completed is final: the order is locked, the actual cost is fixed, a higher reading at service is logged, a preventive order restarts
    /// the vehicle's PM runway, and a linked issue is resolved.</item>
    /// <item>Raising an order from an issue moves the issue to "In progress".</item>
    /// <item>The PM engine run raises one preventive order per vehicle whose PM is due or overdue and has no open preventive order.</item>
    /// </list>
    /// </summary>
    public sealed class WorkOrderService : IWorkOrderService
    {
        private readonly AxpenseDbContext _db;

        public WorkOrderService(AxpenseDbContext db) => _db = db;

        private static DateTime Today => DateTime.UtcNow.Date;
        private static int Active => (int)CurrentStatusType.Active;

        public static string Code(int number) => $"WO-{number:00000}";

        public static async Task<int> NextNumberAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct) =>
            (await db.MaintenanceRecords.Where(x => x.OrganizationId == organizationId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;

        public static string DisplayStatus(Maintenance m) =>
            m.Status == MaintenanceConstants.StatusScheduled && m.DueDate.Date < Today ? MaintenanceConstants.StatusOverdue : m.Status;

        private static string? NextStatus(string status) => status switch
        {
            MaintenanceConstants.StatusScheduled => MaintenanceConstants.StatusInProgress,
            MaintenanceConstants.StatusInProgress => MaintenanceConstants.StatusCompleted,
            _ => null
        };

        // =====================================================================
        // Options / list
        // =====================================================================
        public async Task<WorkOrderOptionsDto> OptionsAsync(Guid organizationId, CancellationToken ct = default)
        {
            var techs = await _db.Users.AsNoTracking().Where(u => u.OrganizationId == organizationId && !u.IsDeleted)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .Select(u => new NamedOption(u.Id, (u.FirstName + " " + u.LastName).Trim() == "" ? u.UserName! : (u.FirstName + " " + u.LastName).Trim()))
                .ToListAsync(ct);
            var parts = await _db.PartCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.CurrentState == Active)
                .OrderBy(c => c.Number).Select(c => new NamedOption(c.Id, c.Name)).ToListAsync(ct);
            var tasks = await _db.TaskCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.CurrentState == Active)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => new NamedOption(c.Id, c.Name)).ToListAsync(ct);
            return new WorkOrderOptionsDto(MaintenanceConstants.Types, MaintenanceConstants.Priorities, MaintenanceConstants.Statuses,
                MaintenanceConstants.DisplayStatuses, techs, parts, tasks);
        }

        public async Task<WorkOrderListResponse> ListAsync(Guid organizationId, WorkOrderListQuery q, CancellationToken ct = default)
        {
            var all = await _db.MaintenanceRecords.AsNoTracking().Include(x => x.Vehicle).Include(x => x.Tasks)
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && (q.VehicleId == null || x.VehicleId == q.VehicleId))
                .ToListAsync(ct);
            var today = Today;
            var rows = all.Select(ToListItem).ToList();

            IEnumerable<WorkOrderListItemDto> f = rows;
            if (!string.IsNullOrWhiteSpace(q.Status)) f = f.Where(r => r.DisplayStatus == q.Status);
            if (!string.IsNullOrWhiteSpace(q.Type)) f = f.Where(r => r.Type == q.Type);
            // Open work first (overdue, in progress, scheduled by date), completed last (newest first).
            var items = f.OrderBy(r => r.DisplayStatus == MaintenanceConstants.StatusCompleted ? 1 : 0)
                .ThenBy(r => r.DisplayStatus == MaintenanceConstants.StatusCompleted ? DateTime.MaxValue - r.ScheduledDate : r.ScheduledDate - DateTime.MinValue)
                .ThenBy(r => r.Code)
                .ToList();

            var open = rows.Where(r => r.Status != MaintenanceConstants.StatusCompleted).ToList();
            var overdue = open.Where(r => r.DisplayStatus == MaintenanceConstants.StatusOverdue).ToList();
            var rules = await PmRules.LoadAsync(_db, organizationId, ct);
            var openIssues = await _db.VehicleIssues.AsNoTracking().CountAsync(i => i.OrganizationId == organizationId && i.CurrentState == Active
                && i.Status != VehicleConstants.IssueResolved && (q.VehicleId == null || i.VehicleId == q.VehicleId), ct);
            var stats = new WorkOrderStatsDto(
                open.Count(r => r.Status == MaintenanceConstants.StatusScheduled && r.ScheduledDate.Date >= today && r.ScheduledDate.Date <= today.AddDays(30)),
                open.Count(r => r.Status == MaintenanceConstants.StatusInProgress),
                overdue.Count,
                overdue.Count == 0 ? null : rules.EscalatedTo(overdue.Max(r => r.DaysOverdue ?? 0)),
                open.Sum(r => r.Total),
                openIssues);
            return new WorkOrderListResponse(items, rows.Count, stats);
        }

        public async Task<ServiceResult<WorkOrderDetailDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            var m = await _db.MaintenanceRecords.AsNoTracking().Include(x => x.Vehicle).Include(x => x.Tasks)
                .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            return m is null ? ServiceResult<WorkOrderDetailDto>.NotFound("Work order not found.") : ServiceResult<WorkOrderDetailDto>.Ok(ToDetail(m));
        }

        // =====================================================================
        // Create / update / status / delete
        // =====================================================================
        public async Task<ServiceResult<WorkOrderDetailDto>> CreateAsync(Guid organizationId, Guid userId, string userName, WorkOrderRequest r, CancellationToken ct = default)
        {
            var (errors, vehicle, lookups) = await ValidateAsync(organizationId, r, null, ct);
            if (errors.Count > 0) return ServiceResult<WorkOrderDetailDto>.Invalid(errors);

            var m = new Maintenance
            {
                OrganizationId = organizationId, VehicleId = vehicle!.Id, Number = await NextNumberAsync(_db, organizationId, ct),
                OdometerAtRaise = vehicle.CurrentOdometer, Status = MaintenanceConstants.StatusScheduled,
                CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
            };
            Apply(m, r, lookups);
            _db.MaintenanceRecords.Add(m);
            _db.MaintenanceTasks.AddRange(BuildTasks(organizationId, userId, m.Id, r, lookups));
            await LinkIssueAsync(organizationId, m, r.IssueId, ct);

            var status = string.IsNullOrWhiteSpace(r.Status) ? MaintenanceConstants.StatusScheduled : r.Status!;
            await _db.SaveChangesAsync(ct);
            if (status != MaintenanceConstants.StatusScheduled)
            {
                var res = await SetStatusAsync(organizationId, userId, userName, m.Id, new WorkOrderStatusRequest { Status = status }, ct);
                if (!res.Succeeded) return res;
            }
            return await GetAsync(organizationId, m.Id, ct);
        }

        public async Task<ServiceResult<WorkOrderDetailDto>> UpdateAsync(Guid organizationId, Guid userId, string userName, Guid id, WorkOrderRequest r, CancellationToken ct = default)
        {
            var m = await _db.MaintenanceRecords.Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (m is null) return ServiceResult<WorkOrderDetailDto>.NotFound("Work order not found.");
            if (m.Status == MaintenanceConstants.StatusCompleted)
                return ServiceResult<WorkOrderDetailDto>.Conflict("WORK_ORDER_COMPLETED", "A completed work order is locked and can't be edited.");
            var (errors, vehicle, lookups) = await ValidateAsync(organizationId, r, m, ct);
            if (errors.Count > 0) return ServiceResult<WorkOrderDetailDto>.Invalid(errors);

            if (m.VehicleId != vehicle!.Id)
            {
                m.VehicleId = vehicle.Id;
                m.OdometerAtRaise = vehicle.CurrentOdometer;
            }
            Apply(m, r, lookups);
            m.UpdatedAt = DateTime.UtcNow;
            m.UpdatedBy = userId;
            _db.MaintenanceTasks.RemoveRange(m.Tasks);
            _db.MaintenanceTasks.AddRange(BuildTasks(organizationId, userId, m.Id, r, lookups));
            if (r.IssueId is not null && r.IssueId != m.IssueId) await LinkIssueAsync(organizationId, m, r.IssueId, ct);
            await _db.SaveChangesAsync(ct);

            var status = string.IsNullOrWhiteSpace(r.Status) ? m.Status : r.Status!;
            if (status != m.Status)
            {
                var res = await SetStatusAsync(organizationId, userId, userName, m.Id, new WorkOrderStatusRequest { Status = status }, ct);
                if (!res.Succeeded) return res;
            }
            return await GetAsync(organizationId, id, ct);
        }

        public async Task<ServiceResult<WorkOrderDetailDto>> SetStatusAsync(Guid organizationId, Guid userId, string userName, Guid id, WorkOrderStatusRequest r, CancellationToken ct = default)
        {
            if (r.Status == MaintenanceConstants.StatusOverdue)
                return ServiceResult<WorkOrderDetailDto>.Invalid(new Dictionary<string, string> { ["status"] = "Overdue is set automatically when a work order passes its scheduled date." });
            if (!MaintenanceConstants.Statuses.Contains(r.Status))
                return ServiceResult<WorkOrderDetailDto>.Invalid(new Dictionary<string, string> { ["status"] = $"Status must be one of: {string.Join(", ", MaintenanceConstants.Statuses)}." });

            var m = await _db.MaintenanceRecords.Include(x => x.Tasks).Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (m is null) return ServiceResult<WorkOrderDetailDto>.NotFound("Work order not found.");
            if (m.Status == r.Status) return ServiceResult<WorkOrderDetailDto>.Ok(ToDetail(m));
            if (m.Status == MaintenanceConstants.StatusCompleted)
                return ServiceResult<WorkOrderDetailDto>.Conflict("WORK_ORDER_COMPLETED", "A completed work order is final.");

            var v = m.Vehicle;
            if (r.Status == MaintenanceConstants.StatusCompleted)
            {
                if (r.Odometer is < 0) return ServiceResult<WorkOrderDetailDto>.Invalid(new Dictionary<string, string> { ["odometer"] = "Odometer can't be negative." });
                var maxReading = await _db.OdometerReadings.Where(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active)
                    .MaxAsync(x => (decimal?)x.Value, ct) ?? 0;
                if (r.Odometer is not null && r.Odometer < (v.CurrentOdometer ?? 0))
                    return ServiceResult<WorkOrderDetailDto>.Invalid(new Dictionary<string, string> { ["odometer"] = $"Reading at service can't be below the current odometer ({v.CurrentOdometer:N0} {v.ReadingUnit})." });

                m.Status = MaintenanceConstants.StatusCompleted;
                m.CompletedAtUtc = DateTime.UtcNow;
                m.StartedAtUtc ??= m.CompletedAtUtc;
                m.ActualCost = m.Tasks.Sum(t => t.Cost);
                m.OdometerAtService = r.Odometer ?? v.CurrentOdometer;
                if (r.Odometer is not null && r.Odometer > maxReading)
                {
                    _db.OdometerReadings.Add(new OdometerReading
                    {
                        OrganizationId = organizationId, VehicleId = v.Id, ReadingDate = Today, Value = r.Odometer.Value,
                        Source = VehicleConstants.ReadingWorkOrder, RecordedBy = string.IsNullOrWhiteSpace(userName) ? null : userName,
                        CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                    });
                    v.CurrentOdometer = r.Odometer;
                }
                if (m.Type == MaintenanceConstants.TypePreventive)
                {
                    v.PmLastServiceReading = m.OdometerAtService ?? v.CurrentOdometer;
                    v.PmLastServiceDate = Today;
                }
                if (m.IssueId is not null)
                {
                    var issue = await _db.VehicleIssues.FirstOrDefaultAsync(i => i.Id == m.IssueId && i.OrganizationId == organizationId, ct);
                    if (issue is not null && issue.Status != VehicleConstants.IssueResolved)
                    {
                        issue.Status = VehicleConstants.IssueResolved;
                        issue.ResolvedDate = Today;
                    }
                }
            }
            else
            {
                m.Status = r.Status;
                if (r.Status == MaintenanceConstants.StatusInProgress) m.StartedAtUtc ??= DateTime.UtcNow;
                if (r.Status == MaintenanceConstants.StatusScheduled) m.StartedAtUtc = null;
            }
            m.UpdatedAt = DateTime.UtcNow;
            m.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);
            return ServiceResult<WorkOrderDetailDto>.Ok(ToDetail(m));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            var m = await _db.MaintenanceRecords.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (m is null) return ServiceResult<bool>.NotFound("Work order not found.");
            _db.MaintenanceRecords.Remove(m);
            await _db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================================
        // PM engine run / fleet issues
        // =====================================================================
        /// <summary>One row per vehicle whose PM is due or overdue, with the library task the engine would raise.</summary>
        private async Task<(PmRules Rules, bool Auto, List<(Vehicle V, PmState Pm, PmTask? Task, Maintenance? Open)> Rows)> PmCandidatesAsync(Guid organizationId, CancellationToken ct)
        {
            await Axpense.Infrastructure.Seeding.OrganizationDefaultsSeeder.SeedPmTasksAsync(_db, organizationId, ct);
            var rules = await PmRules.LoadAsync(_db, organizationId, ct);
            var auto = await _db.PmEngineSettings.AsNoTracking().Where(x => x.OrganizationId == organizationId)
                .Select(x => (bool?)x.AutoGenerateWorkOrders).FirstOrDefaultAsync(ct) ?? true;
            var vehicles = await _db.Vehicles.AsNoTracking()
                .Where(v => v.OrganizationId == organizationId && v.CurrentState == Active && v.Status != VehicleConstants.StatusRetired).ToListAsync(ct);
            var open = await _db.MaintenanceRecords.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.Type == MaintenanceConstants.TypePreventive && x.Status != MaintenanceConstants.StatusCompleted)
                .ToListAsync(ct);
            var library = await _db.PmTasks.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.CurrentState == Active)
                .Include(t => t.PartCategory).Include(t => t.TaskCategory).ToListAsync(ct);
            var rows = new List<(Vehicle, PmState, PmTask?, Maintenance?)>();
            foreach (var v in vehicles)
            {
                var pm = PmCalculator.Evaluate(v, Today, rules);
                if (pm.Status is not ("due" or "overdue") || pm.Lead is null) continue;
                var task = PmTaskService.Match(library, pm.Lead.Unit, pm.Lead.Total);
                rows.Add((v, pm, task, open.Where(o => o.VehicleId == v.Id).OrderBy(o => o.Number).FirstOrDefault()));
            }
            // Overdue first, then by how much of the interval is used.
            rows = rows.OrderBy(r => r.Item2.Status == "overdue" ? 0 : 1).ThenByDescending(r => r.Item2.Percent).ToList();
            return (rules, auto, rows);
        }

        public async Task<PmEnginePreviewDto> PreviewPmEngineAsync(Guid organizationId, CancellationToken ct = default)
        {
            var (rules, auto, rows) = await PmCandidatesAsync(organizationId, ct);
            var items = rows.Select(r => new PmPreviewRowDto(
                r.V.Id, $"{r.V.Make} {r.V.Model}".Trim(), r.V.PlateNumber, r.Pm.Trigger, r.Pm.Lead!.Kind, r.Pm.Lead.Total, r.Pm.Lead.Unit,
                r.Pm.Status, r.Pm.Label, r.Task?.Id, r.Task?.Name ?? DefaultPmTask, r.Task?.Role ?? "Technician", r.Task?.DurationHours, r.Task?.EstimatedCost ?? 0,
                r.Open?.Id, r.Open is null ? null : Code(r.Open.Number), r.Open is null, PmCalculator.DispatchBlocked(r.V, r.Pm, rules))).ToList();
            return new PmEnginePreviewDto(rules.DistancePreAlertKm, rules.TimePreAlertDays, rules.EngineHourPreAlert, auto, rules.BlockDispatchOnCriticalOverdue,
                items, items.Count(i => i.WillCreate), items.Count(i => i.DispatchBlocked), items.Where(i => i.WillCreate).Sum(i => i.EstimatedCost));
        }

        private const string DefaultPmTask = "Preventive maintenance service per PM schedule";

        /// <summary>
        /// Raises one preventive work order per due/overdue vehicle without an open preventive order, using the matched library task
        /// (description, part and task category, estimated cost). Overdue → High priority, scheduled today; due → Medium, scheduled in 3 days.
        /// When <paramref name="vehicleIds"/> is given, only those vehicles are generated.
        /// </summary>
        public async Task<PmEngineRunResult> RunPmEngineAsync(Guid organizationId, Guid userId, IReadOnlyCollection<Guid>? vehicleIds = null, CancellationToken ct = default)
        {
            var (_, _, rows) = await PmCandidatesAsync(organizationId, ct);
            var mechanical = await _db.TaskCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.CurrentState == Active && c.Name == "Mechanical")
                .Select(c => new { c.Id, c.Name }).FirstOrDefaultAsync(ct);
            var number = await NextNumberAsync(_db, organizationId, ct);
            var codes = new List<string>();
            var skipped = 0;
            foreach (var (v, pm, task, open) in rows)
            {
                if (vehicleIds is { Count: > 0 } && !vehicleIds.Contains(v.Id)) continue;
                if (open is not null) { skipped++; continue; }
                var overdue = pm.Status == "overdue";
                var name = task?.Name ?? DefaultPmTask;
                var cost = task?.EstimatedCost ?? 0;
                var m = new Maintenance
                {
                    OrganizationId = organizationId, VehicleId = v.Id, Number = number++, Type = MaintenanceConstants.TypePreventive,
                    Priority = overdue ? MaintenanceConstants.PriorityHigh : MaintenanceConstants.PriorityMedium,
                    Status = MaintenanceConstants.StatusScheduled, DueDate = overdue ? Today : Today.AddDays(3),
                    Description = $"{name} — {pm.Label}", Source = MaintenanceConstants.SourcePmEngine,
                    SourceRef = task is null ? $"{pm.Trigger} trigger" : $"{pm.Trigger} trigger · {task.Name}",
                    OdometerAtRaise = v.CurrentOdometer, EstimatedCost = cost,
                    Notes = task is null ? null : $"Required role: {task.Role} · estimated {task.DurationHours:0.##} h",
                    CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                };
                _db.MaintenanceRecords.Add(m);
                _db.MaintenanceTasks.Add(new MaintenanceTask
                {
                    OrganizationId = organizationId, MaintenanceId = m.Id, Sort = 1, Description = name,
                    PartCategoryId = task?.PartCategoryId, PartCategoryName = task?.PartCategory?.Name,
                    TaskCategoryId = task?.TaskCategoryId ?? mechanical?.Id, TaskCategoryName = task?.TaskCategory?.Name ?? mechanical?.Name,
                    Cost = cost, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                });
                codes.Add(Code(m.Number));
            }
            if (codes.Count > 0) await _db.SaveChangesAsync(ct);
            return new PmEngineRunResult(codes.Count, skipped, codes);
        }

        public async Task<List<FleetIssueDto>> IssuesAsync(Guid organizationId, string? status, CancellationToken ct = default)
        {
            var rows = await _db.VehicleIssues.AsNoTracking()
                .Where(i => i.OrganizationId == organizationId && i.CurrentState == Active && i.Vehicle.CurrentState == Active
                            && (status == null || status == "" || i.Status == status))
                .Select(i => new
                {
                    i.Id, i.Number, i.VehicleId, i.Vehicle.Make, i.Vehicle.Model, i.Vehicle.PlateNumber, i.Title, i.Note,
                    PartName = i.PresetPart != null ? i.PresetPart.Name : null, i.Priority, i.Source, i.Status, i.ReportedDate
                })
                .ToListAsync(ct);
            var ids = rows.Select(r => (Guid?)r.Id).ToList();
            var orders = await _db.MaintenanceRecords.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && m.CurrentState == Active && ids.Contains(m.IssueId))
                .Select(m => new { m.IssueId, m.Id, m.Number }).ToListAsync(ct);
            return rows
                .OrderBy(r => r.Status == VehicleConstants.IssueResolved ? 1 : 0)
                .ThenBy(r => r.Priority == VehicleConstants.PriorityHigh ? 0 : r.Priority == VehicleConstants.PriorityMedium ? 1 : 2)
                .ThenByDescending(r => r.ReportedDate)
                .Select(r =>
                {
                    var wo = orders.Where(o => o.IssueId == r.Id).OrderByDescending(o => o.Number).FirstOrDefault();
                    return new FleetIssueDto(r.Id, $"ISS-{r.Number:0000}", r.VehicleId, $"{r.Make} {r.Model}".Trim(), r.PlateNumber, r.Title, r.Note,
                        r.PartName, r.Priority, r.Source, r.Status, r.ReportedDate, wo?.Id, wo is null ? null : Code(wo.Number));
                })
                .ToList();
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private sealed record Lookups(Dictionary<Guid, string> PartCategories, Dictionary<Guid, string> TaskCategories, string? TechnicianName);

        private async Task<(Dictionary<string, string> Errors, Vehicle? Vehicle, Lookups Lookups)> ValidateAsync(Guid organizationId, WorkOrderRequest r, Maintenance? existing, CancellationToken ct)
        {
            var e = new Dictionary<string, string>();
            Vehicle? v = null;
            if (r.VehicleId is null) e["vehicleId"] = "Select a vehicle.";
            else
            {
                v = await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.VehicleId && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
                if (v is null) e["vehicleId"] = "Vehicle not found.";
                else if (v.Status == VehicleConstants.StatusRetired && existing?.VehicleId != v.Id) e["vehicleId"] = "A retired vehicle can't get new work orders.";
            }
            if (string.IsNullOrWhiteSpace(r.Type)) e["type"] = "Type is required.";
            else if (!MaintenanceConstants.Types.Contains(r.Type)) e["type"] = $"Type must be one of: {string.Join(", ", MaintenanceConstants.Types)}.";
            if (!string.IsNullOrWhiteSpace(r.Priority) && !MaintenanceConstants.Priorities.Contains(r.Priority)) e["priority"] = $"Priority must be one of: {string.Join(", ", MaintenanceConstants.Priorities)}.";
            if (r.Status == MaintenanceConstants.StatusOverdue) e["status"] = "Overdue is set automatically when a work order passes its scheduled date.";
            else if (!string.IsNullOrWhiteSpace(r.Status) && !MaintenanceConstants.Statuses.Contains(r.Status)) e["status"] = $"Status must be one of: {string.Join(", ", MaintenanceConstants.Statuses)}.";
            if (r.ScheduledDate is null) e["scheduledDate"] = "Scheduled date is required.";
            var d = r.Description?.Trim() ?? "";
            if (d.Length == 0) e["description"] = "Describe what is being done and why.";
            else if (d.Length > 500) e["description"] = "Description must be 500 characters or fewer.";
            if (r.Notes is { Length: > 2000 }) e["notes"] = "Notes must be 2000 characters or fewer.";

            string? techName = null;
            if (r.TechnicianUserId is not null)
            {
                var u = await _db.Users.AsNoTracking().Where(x => x.Id == r.TechnicianUserId && x.OrganizationId == organizationId && !x.IsDeleted)
                    .Select(x => new { x.FirstName, x.LastName, x.UserName }).FirstOrDefaultAsync(ct);
                if (u is null) e["technicianUserId"] = "Technician not found.";
                else techName = $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } n ? n : u.UserName;
            }

            var partCats = await _db.PartCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.CurrentState == Active).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
            var taskCats = await _db.TaskCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.CurrentState == Active).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
            if (r.Tasks is null || r.Tasks.Count == 0) e["tasks"] = "Add at least one task.";
            else if (r.Tasks.Count > MaintenanceConstants.MaxTasks) e["tasks"] = $"A work order can have at most {MaintenanceConstants.MaxTasks} tasks.";
            else
                for (var i = 0; i < r.Tasks.Count; i++)
                {
                    var t = r.Tasks[i];
                    var k = $"tasks[{i}]";
                    var td = t.Description?.Trim() ?? "";
                    if (td.Length == 0) e[$"{k}.description"] = $"Task {i + 1}: describe the task.";
                    else if (td.Length > 200) e[$"{k}.description"] = $"Task {i + 1}: 200 characters or fewer.";
                    if (t.Cost is < 0) e[$"{k}.cost"] = $"Task {i + 1}: cost can't be negative.";
                    if (t.PartCategoryId is not null && !partCats.ContainsKey(t.PartCategoryId.Value)) e[$"{k}.partCategoryId"] = $"Task {i + 1}: part category not found.";
                    if (t.TaskCategoryId is not null && !taskCats.ContainsKey(t.TaskCategoryId.Value)) e[$"{k}.taskCategoryId"] = $"Task {i + 1}: task category not found.";
                }
            if (r.IssueId is not null && v is not null &&
                !await _db.VehicleIssues.AnyAsync(i => i.Id == r.IssueId && i.OrganizationId == organizationId && i.VehicleId == v.Id && i.CurrentState == Active, ct))
                e["issueId"] = "Issue not found on this vehicle.";
            return (e, v, new Lookups(partCats, taskCats, techName));
        }

        private static void Apply(Maintenance m, WorkOrderRequest r, Lookups l)
        {
            m.Type = r.Type!;
            m.Priority = string.IsNullOrWhiteSpace(r.Priority) ? MaintenanceConstants.PriorityMedium : r.Priority!;
            m.DueDate = r.ScheduledDate!.Value.Date;
            m.TechnicianUserId = r.TechnicianUserId;
            m.TechnicianName = l.TechnicianName;
            m.Description = r.Description!.Trim();
            m.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
            m.EstimatedCost = r.Tasks.Sum(t => t.Cost ?? 0);
        }

        private static IEnumerable<MaintenanceTask> BuildTasks(Guid organizationId, Guid userId, Guid maintenanceId, WorkOrderRequest r, Lookups l) =>
            r.Tasks.Select((t, i) => new MaintenanceTask
            {
                OrganizationId = organizationId, MaintenanceId = maintenanceId, Sort = i + 1, Description = t.Description!.Trim(),
                PartCategoryId = t.PartCategoryId, PartCategoryName = t.PartCategoryId is null ? null : l.PartCategories[t.PartCategoryId.Value],
                TaskCategoryId = t.TaskCategoryId, TaskCategoryName = t.TaskCategoryId is null ? null : l.TaskCategories[t.TaskCategoryId.Value],
                Cost = t.Cost ?? 0, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
            });

        private async Task LinkIssueAsync(Guid organizationId, Maintenance m, Guid? issueId, CancellationToken ct)
        {
            if (issueId is null) return;
            var issue = await _db.VehicleIssues.FirstOrDefaultAsync(i => i.Id == issueId && i.OrganizationId == organizationId, ct);
            if (issue is null) return;
            m.IssueId = issue.Id;
            m.Source = MaintenanceConstants.SourceIssue;
            m.SourceRef = $"ISS-{issue.Number:0000}";
            if (issue.Status == VehicleConstants.IssueOpen) issue.Status = VehicleConstants.IssueInProgress;
        }

        private static WorkOrderListItemDto ToListItem(Maintenance m)
        {
            var display = DisplayStatus(m);
            return new WorkOrderListItemDto(m.Id, Code(m.Number), m.VehicleId, $"{m.Vehicle.Make} {m.Vehicle.Model}".Trim(), m.Vehicle.PlateNumber,
                m.Type, m.Priority, m.Status, display, display == MaintenanceConstants.StatusOverdue ? (Today - m.DueDate.Date).Days : null,
                m.DueDate, m.TechnicianName, Total(m), m.Tasks.Count, m.Description, m.Source, m.SourceRef);
        }

        private static decimal Total(Maintenance m) =>
            m.Status == MaintenanceConstants.StatusCompleted && m.ActualCost is not null ? m.ActualCost.Value : m.Tasks.Count > 0 ? m.Tasks.Sum(t => t.Cost) : m.EstimatedCost ?? 0;

        private static WorkOrderDetailDto ToDetail(Maintenance m)
        {
            var display = DisplayStatus(m);
            return new WorkOrderDetailDto(m.Id, Code(m.Number), m.VehicleId, $"{m.Vehicle.Make} {m.Vehicle.Model}".Trim(), m.Vehicle.PlateNumber, m.Vehicle.ReadingUnit,
                m.Type, m.Priority, m.Status, display, display == MaintenanceConstants.StatusOverdue ? (Today - m.DueDate.Date).Days : null, NextStatus(m.Status),
                m.DueDate, m.TechnicianUserId, m.TechnicianName, m.Description, m.Notes, m.Source, m.SourceRef, m.IssueId, m.InspectionId,
                m.OdometerAtRaise, m.OdometerAtService, Total(m), m.ActualCost, m.StartedAtUtc, m.CompletedAtUtc,
                m.Tasks.OrderBy(t => t.Sort).Select(t => new WorkOrderTaskDto(t.Id, t.Sort, t.Description, t.PartCategoryId, t.PartCategoryName,
                    t.TaskCategoryId, t.TaskCategoryName, t.Cost)).ToList());
        }
    }
}
