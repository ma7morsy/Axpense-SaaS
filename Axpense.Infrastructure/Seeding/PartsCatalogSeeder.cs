using Axpense.Data.Constants;
using Axpense.Data.Entities.CatalogEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Infrastructure.Seeding
{
    /// <summary>Gives an organization the default parts catalogue if it has no categories yet.</summary>
    public static class PartsCatalogSeeder
    {
        public static async Task<bool> SeedIfEmptyAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct = default)
        {
            if (await db.PartCategories.AnyAsync(c => c.OrganizationId == organizationId, ct)) return false;

            var now = DateTime.UtcNow;
            var number = 0;
            foreach (var c in PartsCatalogDefaults.Categories)
            {
                number++;
                var category = new PartCategory
                {
                    OrganizationId = organizationId, Number = number, Name = c.Name, NameAr = c.NameAr,
                    CurrentState = (int)CurrentStatusType.Active, CreatedAt = now
                };
                var seq = 0;
                foreach (var p in c.Parts)
                {
                    seq++;
                    category.Parts.Add(new PresetPart
                    {
                        OrganizationId = organizationId, Sequence = seq, Code = PartsCatalogDefaults.PartCode(number, seq),
                        Name = p.Name, NameAr = p.NameAr, CurrentState = (int)CurrentStatusType.Active, CreatedAt = now
                    });
                }
                db.PartCategories.Add(category);
            }
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
