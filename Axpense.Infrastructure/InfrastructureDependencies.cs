using Axpense.Data.UserApplication;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Infrastructure.Repositories;
using Axpense.Infrastructure.Storage;
using Axpense.Infrastructure.Telematics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axpense.Infrastructure
{
    public static class InfrastructureDependencies
    {
        public static IServiceCollection AddInfrastructureDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AxpenseDbContext>(options =>
                  options.UseNpgsql(configuration.GetConnectionString("DefaultConnection") ??
                  throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

            // ASP.NET Identity - عشان User/Role Management والـ Authentication
            //
            // ملحوظة: بنستخدم AddIdentityCore مش AddIdentity، لأن AddIdentity (بالنسخة الكاملة
            // اللي بتضيف كوكيز/تسجيل دخول بالمتصفح) موجودة في الـ Shared Framework بتاع
            // ASP.NET Core (Microsoft.AspNetCore.App)، وده مش متاح في مشروع Class Library
            // زي infrastructure ده. إحنا أصلًا مش محتاجين حاجة من ده لأننا بنستخدم JWT بس،
            // فـ AddIdentityCore + AddRoles هو الصح هنا وأخف كمان.
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                // إعدادات باسورد بسيطة مناسبة لمرحلة التطوير - ممكن نشددها بعدين
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;

                options.User.RequireUniqueEmail = true;

                // قفل الحساب بعد 5 محاولات فاشلة لمدة 5 دقايق
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AxpenseDbContext>()
            .AddDefaultTokenProviders();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped(typeof(IVwRepository<>), typeof(VwRepository<>));
            services.AddSingleton<IFileStorage, LocalFileStorage>();
            // Live telemetry: simulated until a GPS/telematics vendor adapter is registered here.
            services.AddSingleton<ITelematicsProvider, SimulatedTelematicsProvider>();
            return services;
        }
    }
}
