using Axpense.Data.Constants;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Common;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Settings
{
    public sealed record PmTaskDto(Guid Id, string Name, Guid? PartCategoryId, string? PartCategoryName, Guid? TaskCategoryId, string? TaskCategoryName,
        string Trigger, int? IntervalKm, int? IntervalDays, int? IntervalHours, decimal DurationHours, decimal EstimatedCost, string Role, bool IsActive, int SortOrder);

    public sealed record PmTaskRequest(string? Name, Guid? PartCategoryId, Guid? TaskCategoryId, string? Trigger, int? IntervalKm, int? IntervalDays,
        int? IntervalHours, decimal? DurationHours, decimal? EstimatedCost, string? Role, bool? IsActive);

    public interface IPmTaskService
    {
        Task<IReadOnlyList<PmTaskDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<PmTaskDto>> CreateAsync(Guid organizationId, Guid userId, PmTaskRequest request, CancellationToken ct = default);
        Task<ServiceResult<PmTaskDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, PmTaskRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    }

    /// <summary>
    /// PM task library. Rules: name 1–120 characters and unique per organization; trigger usage / time / hours / hybrid;
    /// the trigger decides which interval is required (usage → km, time → days, hours → engine hours, hybrid → km and days);
    /// intervals are positive; duration 0.25–100 h; cost 0–10,000,000; part and task categories must belong to the organization.
    /// Work orders copy the task text and cost, so deleting a task never changes existing orders.
    /// </summary>
    public sealed class PmTaskService : IPmTaskService
    {
        private readonly AxpenseDbContext _db;
        public PmTaskService(AxpenseDbContext db) => _db = db;
        private static int Active => (int)CurrentStatusType.Active;

        public static IQueryable<PmTaskDto> Project(IQueryable<PmTask> q) => q.Select(t => new PmTaskDto(t.Id, t.Name, t.PartCategoryId,
            t.PartCategory != null ? t.PartCategory.Name : null, t.TaskCategoryId, t.TaskCategory != null ? t.TaskCategory.Name : null,
            t.Trigger, t.IntervalKm, t.IntervalDays, t.IntervalHours, t.DurationHours, t.EstimatedCost, t.Role, t.IsActive, t.SortOrder));

        public async Task<IReadOnlyList<PmTaskDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
        {
            await OrganizationDefaultsSeeder.SeedPmTasksAsync(_db, organizationId, ct);
            return await Project(_db.PmTasks.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.CurrentState == Active)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)).ToListAsync(ct);
        }

        private async Task<Dictionary<string, string>> ValidateAsync(Guid organizationId, Guid? id, PmTaskRequest r, CancellationToken ct)
        {
            var e = new Dictionary<string, string>();
            var name = r.Name?.Trim() ?? "";
            if (name.Length == 0) e["name"] = "Task name is required.";
            else if (name.Length > 120) e["name"] = "Task name must be 120 characters or fewer.";
            else if (await _db.PmTasks.AnyAsync(t => t.OrganizationId == organizationId && t.CurrentState == Active && t.Id != id && t.Name.ToLower() == name.ToLower(), ct))
                e["name"] = $"A PM task named {name} already exists.";

            var trigger = r.Trigger ?? "";
            if (!SettingsDefaults.PmTaskTriggers.Contains(trigger)) e["trigger"] = "Choose a trigger type.";
            else
            {
                if (trigger is "usage" or "hybrid" && r.IntervalKm is null) e["intervalKm"] = "Distance interval is required for this trigger.";
                if (trigger is "time" or "hybrid" && r.IntervalDays is null) e["intervalDays"] = "Time interval is required for this trigger.";
                if (trigger == "hours" && r.IntervalHours is null) e["intervalHours"] = "Engine hour interval is required for this trigger.";
            }
            if (r.IntervalKm is <= 0 or > 2_000_000) e["intervalKm"] = "Distance interval must be between 1 and 2,000,000 km.";
            if (r.IntervalDays is <= 0 or > 3650) e["intervalDays"] = "Time interval must be between 1 and 3,650 days.";
            if (r.IntervalHours is <= 0 or > 100_000) e["intervalHours"] = "Engine hour interval must be between 1 and 100,000.";
            if (r.DurationHours is null || r.DurationHours < 0.25m || r.DurationHours > 100) e["durationHours"] = "Duration must be between 0.25 and 100 hours.";
            if (r.EstimatedCost is null || r.EstimatedCost < 0 || r.EstimatedCost > 10_000_000) e["estimatedCost"] = "Estimated cost must be between 0 and 10,000,000.";
            if ((r.Role?.Trim().Length ?? 0) > 80) e["role"] = "Role must be 80 characters or fewer.";
            if (r.PartCategoryId is { } pc && !await _db.PartCategories.AnyAsync(c => c.Id == pc && c.OrganizationId == organizationId && c.CurrentState == Active, ct))
                e["partCategoryId"] = "Choose a part category from the catalogue.";
            if (r.TaskCategoryId is { } tc && !await _db.TaskCategories.AnyAsync(c => c.Id == tc && c.OrganizationId == organizationId && c.CurrentState == Active, ct))
                e["taskCategoryId"] = "Choose a task category.";
            return e;
        }

        private static void Apply(PmTask t, PmTaskRequest r)
        {
            t.Name = r.Name!.Trim();
            t.Trigger = r.Trigger!;
            // Keep only the intervals the trigger uses.
            t.IntervalKm = r.Trigger is "usage" or "hybrid" ? r.IntervalKm : null;
            t.IntervalDays = r.Trigger is "time" or "hybrid" ? r.IntervalDays : null;
            t.IntervalHours = r.Trigger == "hours" ? r.IntervalHours : null;
            t.DurationHours = r.DurationHours!.Value;
            t.EstimatedCost = r.EstimatedCost!.Value;
            t.Role = string.IsNullOrWhiteSpace(r.Role) ? "Technician" : r.Role.Trim();
            t.PartCategoryId = r.PartCategoryId;
            t.TaskCategoryId = r.TaskCategoryId;
            t.IsActive = r.IsActive ?? true;
        }

        private async Task<PmTaskDto> GetDtoAsync(Guid id, CancellationToken ct) =>
            await Project(_db.PmTasks.AsNoTracking().Where(t => t.Id == id)).FirstAsync(ct);

        public async Task<ServiceResult<PmTaskDto>> CreateAsync(Guid organizationId, Guid userId, PmTaskRequest request, CancellationToken ct = default)
        {
            var e = await ValidateAsync(organizationId, null, request, ct);
            if (e.Count > 0) return ServiceResult<PmTaskDto>.Invalid(e);
            var order = (await _db.PmTasks.Where(t => t.OrganizationId == organizationId).MaxAsync(t => (int?)t.SortOrder, ct) ?? 0) + 1;
            var t = new PmTask { OrganizationId = organizationId, SortOrder = order, CurrentState = Active, CreatedBy = userId, CreatedAt = DateTime.UtcNow };
            Apply(t, request);
            _db.PmTasks.Add(t);
            await _db.SaveChangesAsync(ct);
            return ServiceResult<PmTaskDto>.Ok(await GetDtoAsync(t.Id, ct));
        }

        public async Task<ServiceResult<PmTaskDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, PmTaskRequest request, CancellationToken ct = default)
        {
            var t = await _db.PmTasks.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (t is null) return ServiceResult<PmTaskDto>.NotFound("PM task not found.");
            var e = await ValidateAsync(organizationId, id, request, ct);
            if (e.Count > 0) return ServiceResult<PmTaskDto>.Invalid(e);
            Apply(t, request);
            t.UpdatedAt = DateTime.UtcNow;
            t.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);
            return ServiceResult<PmTaskDto>.Ok(await GetDtoAsync(t.Id, ct));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            var t = await _db.PmTasks.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (t is null) return ServiceResult<bool>.NotFound("PM task not found.");
            _db.PmTasks.Remove(t);
            await _db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        /// <summary>
        /// Picks the library task for a vehicle whose PM is due: the first active task (library order) with an interval in the unit
        /// of the leading leg; otherwise the first active task. Null when the library is empty. Reorder the library to change the pick.
        /// </summary>
        public static PmTask? Match(IReadOnlyList<PmTask> library, string leadUnit, decimal leadTotal)
        {
            var active = library.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList();
            int? Of(PmTask t) => leadUnit switch { "km" => t.IntervalKm, "days" => t.IntervalDays, "hr" => t.IntervalHours, _ => null };
            var byUnit = active.Where(t => Of(t) is not null).ToList();
            return byUnit.FirstOrDefault() ?? active.FirstOrDefault();
        }
    }
}
