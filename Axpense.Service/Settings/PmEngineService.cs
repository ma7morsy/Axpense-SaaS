using Axpense.Data.Constants;
using Axpense.Data.Entities.SaasEntities;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Common;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Settings
{
    public interface IPmEngineService
    {
        Task<PmEngineDto> GetAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<PmEngineDto>> UpdateAsync(Guid organizationId, Guid userId, PmEngineRequest request, CancellationToken ct = default);
    }

    /// <summary>The PM engine rules other modules evaluate against (pre-alerts, dispatch blocking, escalation).</summary>
    public sealed record PmRules(int DistancePreAlertKm, int TimePreAlertDays, int EngineHourPreAlert, bool BlockDispatchOnCriticalOverdue,
        IReadOnlyList<(int AfterDays, string Role)> Escalation)
    {
        public static readonly PmRules Default = new(500, 7, 25, true, [(0, "Technician"), (3, "Supervisor"), (7, "Fleet manager")]);

        public static async Task<PmRules> LoadAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct)
        {
            var s = await db.PmEngineSettings.AsNoTracking().Include(x => x.EscalationSteps)
                .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
            return s is null
                ? Default
                : new PmRules(s.DistancePreAlertKm, s.TimePreAlertDays, s.EngineHourPreAlert, s.BlockDispatchOnCriticalOverdue,
                    s.EscalationSteps.OrderBy(x => x.AfterDaysOverdue).Select(x => (x.AfterDaysOverdue, x.Role)).ToList());
        }

        /// <summary>Role an item overdue by this many days is escalated to.</summary>
        public string EscalatedTo(int daysOverdue) =>
            Escalation.Where(x => daysOverdue >= x.AfterDays).Select(x => x.Role).LastOrDefault() ?? Escalation.FirstOrDefault().Role ?? "Technician";
    }

    /// <summary>
    /// PM engine rules. Pre-alerts drive the vehicle PM runway (due / overdue) and dispatch blocking today; auto work
    /// orders, dispatch blocking and escalation are stored now and enforced by the Work orders module.
    /// </summary>
    public sealed class PmEngineService : IPmEngineService
    {
        private readonly AxpenseDbContext _db;

        public PmEngineService(AxpenseDbContext db) => _db = db;

        public async Task<PmEngineDto> GetAsync(Guid organizationId, CancellationToken ct = default)
        {
            var s = await LoadAsync(organizationId, ct);
            return ToDto(s);
        }

        public async Task<ServiceResult<PmEngineDto>> UpdateAsync(Guid organizationId, Guid userId, PmEngineRequest r, CancellationToken ct = default)
        {
            var e = new Dictionary<string, string>();
            if (r.DistancePreAlertKm is < 0 or > 100_000) e["distancePreAlertKm"] = "Distance pre-alert must be between 0 and 100,000 km.";
            if (r.TimePreAlertDays is < 0 or > 365) e["timePreAlertDays"] = "Time pre-alert must be between 0 and 365 days.";
            if (r.EngineHourPreAlert is < 0 or > 10_000) e["engineHourPreAlert"] = "Engine-hour pre-alert must be between 0 and 10,000.";

            var steps = r.EscalationSteps ?? [];
            if (steps.Count is < 1 or > 6) e["escalationSteps"] = "The escalation chain needs 1 to 6 steps.";
            else if (steps.Any(x => x.AfterDaysOverdue is < 0 or > 365)) e["escalationSteps"] = "Escalation days must be between 0 and 365.";
            else if (steps.Select(x => x.AfterDaysOverdue).Distinct().Count() != steps.Count) e["escalationSteps"] = "Each escalation step needs a different number of days.";
            else if (steps.Any(x => !SettingsDefaults.EscalationRoles.Contains(x.Role))) e["escalationSteps"] = "Pick a role for every escalation step.";
            if (e.Count > 0) return ServiceResult<PmEngineDto>.Invalid(e);

            var s = await LoadAsync(organizationId, ct, tracked: true);
            s.DistancePreAlertKm = r.DistancePreAlertKm;
            s.TimePreAlertDays = r.TimePreAlertDays;
            s.EngineHourPreAlert = r.EngineHourPreAlert;
            s.AutoGenerateWorkOrders = r.AutoGenerateWorkOrders;
            s.BlockDispatchOnCriticalOverdue = r.BlockDispatchOnCriticalOverdue;
            s.UpdatedBy = userId;
            s.UpdatedAt = DateTime.UtcNow;

            // Replace the chain. New rows are added explicitly: entity ids are generated client-side,
            // so EF would otherwise treat them as existing rows to update.
            _db.PmEscalationSteps.RemoveRange(s.EscalationSteps.ToList());
            var chain = steps.OrderBy(x => x.AfterDaysOverdue).Select(x => new PmEscalationStep
            {
                OrganizationId = organizationId, PmEngineSettingsId = s.Id, AfterDaysOverdue = x.AfterDaysOverdue, Role = x.Role,
                CurrentState = (int)CurrentStatusType.Active, CreatedBy = userId, CreatedAt = DateTime.UtcNow
            }).ToList();
            _db.PmEscalationSteps.AddRange(chain);
            _db.AuditLogs.Add(new AuditLog
            {
                OrganizationId = organizationId, UserId = userId, Action = "Updated", EntityType = nameof(PmEngineSettings),
                EntityId = s.Id.ToString(), Details = "PM engine rules saved", CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            return ServiceResult<PmEngineDto>.Ok(await GetAsync(organizationId, ct));
        }

        private async Task<PmEngineSettings> LoadAsync(Guid organizationId, CancellationToken ct, bool tracked = false)
        {
            await OrganizationDefaultsSeeder.SeedPmEngineAsync(_db, organizationId, ct);
            var q = _db.PmEngineSettings.Include(x => x.EscalationSteps).Where(x => x.OrganizationId == organizationId);
            return await (tracked ? q : q.AsNoTracking()).FirstAsync(ct);
        }

        private static PmEngineDto ToDto(PmEngineSettings s) => new(
            s.DistancePreAlertKm, s.TimePreAlertDays, s.EngineHourPreAlert, s.AutoGenerateWorkOrders, s.BlockDispatchOnCriticalOverdue,
            s.EscalationSteps.OrderBy(x => x.AfterDaysOverdue).Select(x => new EscalationStepDto(x.AfterDaysOverdue, x.Role)).ToList(),
            SettingsDefaults.EscalationRoles);
    }
}
