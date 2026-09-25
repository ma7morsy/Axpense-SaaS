using Axpense.Data.Entities;
using Axpense.Data.UserApplication;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Axpense.Infrastructure
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var db = serviceProvider.GetRequiredService<AxpenseDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var roleName in new[] { "Owner", "Admin", "Manager", "Staff" })
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }

            // Every organization gets its default configuration once (idempotent).
            foreach (var orgId in await db.Organizations.Select(o => o.Id).ToListAsync())
                await OrganizationDefaultsSeeder.SeedAsync(db, orgId);

            if (await userManager.FindByNameAsync("admin") != null)
                return;

            var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Name == "Axpense Demo Organization");
            if (organization == null)
            {
                organization = new Organization { Name = "Axpense Demo Organization" };
                db.Organizations.Add(organization);
                await db.SaveChangesAsync();
            }

            var user = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@axpense.local",
                EmailConfirmed = true,
                OrganizationId = organization.Id,
                FirstName = "Axpense",
                LastName = "Admin"
            };

            var result = await userManager.CreateAsync(user, "Axpense123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Owner");
            }
            await OrganizationDefaultsSeeder.SeedAsync(db, organization.Id);
        }
    }
}
