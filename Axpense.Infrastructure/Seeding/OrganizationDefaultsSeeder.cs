using Axpense.Data.Constants;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Infrastructure.Seeding
{
    /// <summary>
    /// Gives an organization its default configuration: parts catalogue, expense types, task
    /// categories and PM engine rules. Each part is idempotent — it only runs when missing.
    /// </summary>
    public static class OrganizationDefaultsSeeder
    {
        public static async Task SeedAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct = default)
        {
            await PartsCatalogSeeder.SeedIfEmptyAsync(db, organizationId, ct);
            await SeedExpenseTypesAsync(db, organizationId, ct);
            await SeedTaskCategoriesAsync(db, organizationId, ct);
            await SeedPmEngineAsync(db, organizationId, ct);
            await SeedPmTasksAsync(db, organizationId, ct);
        }

        /// <summary>Default PM task library, linked to the seeded part and task categories by name.</summary>
        public static async Task SeedPmTasksAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct = default)
        {
            if (await db.PmTasks.AnyAsync(x => x.OrganizationId == organizationId, ct)) return;
            var parts = await db.PartCategories.Where(c => c.OrganizationId == organizationId).Select(c => new { c.Id, c.Name }).ToListAsync(ct);
            var tasks = await db.TaskCategories.Where(c => c.OrganizationId == organizationId).Select(c => new { c.Id, c.Name }).ToListAsync(ct);
            var order = 0;
            foreach (var t in SettingsDefaults.PmTasks)
                db.PmTasks.Add(new PmTask
                {
                    OrganizationId = organizationId, Name = t.Name, Trigger = t.Trigger, IntervalKm = t.Km, IntervalDays = t.Days, IntervalHours = t.Hours,
                    DurationHours = t.Duration, EstimatedCost = t.Cost, Role = t.Role, SortOrder = ++order,
                    PartCategoryId = parts.FirstOrDefault(p => p.Name == t.PartCategory)?.Id,
                    TaskCategoryId = tasks.FirstOrDefault(p => p.Name == t.TaskCategory)?.Id,
                    CurrentState = (int)CurrentStatusType.Active
                });
            await db.SaveChangesAsync(ct);
        }

        public static async Task SeedExpenseTypesAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct = default)
        {
            if (await db.ExpenseTypes.AnyAsync(x => x.OrganizationId == organizationId, ct)) return;
            var order = 0;
            foreach (var t in SettingsDefaults.ExpenseTypes)
                db.ExpenseTypes.Add(new ExpenseType
                {
                    OrganizationId = organizationId, Name = t.Name, Color = t.Color, SystemKey = t.SystemKey,
                    SortOrder = ++order, CurrentState = (int)CurrentStatusType.Active
                });
            await db.SaveChangesAsync(ct);
        }

        public static async Task SeedTaskCategoriesAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct = default)
        {
            if (await db.TaskCategories.AnyAsync(x => x.OrganizationId == organizationId, ct)) return;
            var order = 0;
            foreach (var name in SettingsDefaults.TaskCategories)
                db.TaskCategories.Add(new TaskCategory
                {
                    OrganizationId = organizationId, Name = name, SortOrder = ++order, CurrentState = (int)CurrentStatusType.Active
                });
            await db.SaveChangesAsync(ct);
        }

        public static async Task SeedPmEngineAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct = default)
        {
            if (await db.PmEngineSettings.AnyAsync(x => x.OrganizationId == organizationId, ct)) return;
            var settings = new PmEngineSettings { OrganizationId = organizationId, CurrentState = (int)CurrentStatusType.Active };
            foreach (var (after, role) in SettingsDefaults.EscalationChain)
                settings.EscalationSteps.Add(new PmEscalationStep
                {
                    OrganizationId = organizationId, AfterDaysOverdue = after, Role = role, CurrentState = (int)CurrentStatusType.Active
                });
            db.PmEngineSettings.Add(settings);
            await db.SaveChangesAsync(ct);
        }
    }
}
