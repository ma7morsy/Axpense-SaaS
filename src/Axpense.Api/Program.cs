using MediatR;
using System.Threading.RateLimiting;
using Axpense.Api;
using Axpense.Infrastructure;
using Axpense.Infrastructure.Context;
using Axpense.Service.Auth;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

// Npgsql 6+ refuses to write a DateTime with Kind=Unspecified into a "timestamp with time zone"
// column (throws "Cannot write DateTime with Kind=Unspecified ... only UTC is supported").
// Every date coming from the frontend (e.g. an <input type="date"> value like "2026-09-22", or any
// JSON date string without a timezone offset) deserializes via System.Text.Json as Kind=Unspecified,
// which is exactly what broke every "Create" endpoint (Maintenance, Inspections, Fuel, Budgets, ...).
// This restores Npgsql's pre-v6 lenient behavior: Unspecified is treated as UTC instead of throwing.
// Must be set before any NpgsqlConnection/DbContext is used, so it goes at the very top of Program.cs.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Several entities have bidirectional navigation properties (e.g. Inspection.Items <->
        // InspectionItem.Inspection). EF Core's change tracker "fixes up" both sides of the
        // relationship in memory even when only one side was explicitly set, so returning a raw
        // tracked entity from a controller (e.g. `return Created(url, entity)`) can serialize into
        // an infinite Parent -> Children -> Parent -> Children loop, throwing
        // "A possible object cycle was detected" instead of a 500 from a real bug.
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 120;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
        o.AutoReplenishment = true;
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructureDependencies(builder.Configuration);
builder.Services.AddMediatR(cfg =>
{
    // Scan both the assembly that DEFINES the open-generic handlers (Axpense.Core) and the
    // assembly that defines the entity types used to CLOSE them (Axpense.Data, e.g. BaseEntity,
    // Vehicle, Notification...). MediatR's generic-handler closing logic finds concrete closing
    // types by scanning the registered assemblies for classes satisfying the handler's generic
    // constraints (T : BaseEntity) -- it does not look for usages in controllers. Since the
    // entity classes live in Axpense.Data, that assembly must be scanned too, or zero concrete
    // types are found and no closed handler ever gets registered.
    cfg.RegisterServicesFromAssemblies(
        typeof(Axpense.Core.CoreDependencies).Assembly,
        typeof(Axpense.Data.Entities.BaseEntity).Assembly);
    // MediatR 12.4.1+ made auto-registration of open-generic handlers (e.g. GetListwithFilterQueryHandler<T>) opt-in.
    // Without this, every generic query/command handler fails DI resolution with
    // "No service for type 'MediatR.IRequestHandler...' has been registered."
    cfg.RegisterGenericHandlers = true;
});
builder.Services.AddScoped<IAuthService, Axpense.Service.Auth.AuthService>();
builder.Services.AddScoped<Axpense.Service.Drivers.IDriverService, Axpense.Service.Drivers.DriverService>();
builder.Services.AddScoped<Axpense.Service.Vehicles.IVehicleService, Axpense.Service.Vehicles.VehicleService>();
builder.Services.AddScoped<Axpense.Service.Vehicles.IVehicleRecordsService, Axpense.Service.Vehicles.VehicleRecordsService>();
builder.Services.AddScoped<Axpense.Service.Inspections.IInspectionService, Axpense.Service.Inspections.InspectionService>();
builder.Services.AddScoped<Axpense.Service.WorkOrders.IWorkOrderService, Axpense.Service.WorkOrders.WorkOrderService>();
builder.Services.AddScoped<Axpense.Service.Inspections.IInspectionTemplateService, Axpense.Service.Inspections.InspectionTemplateService>();
builder.Services.AddScoped<Axpense.Service.Tracking.IFleetTrackingService, Axpense.Service.Tracking.FleetTrackingService>();
builder.Services.AddScoped<Axpense.Service.Catalog.IPartsCatalogService, Axpense.Service.Catalog.PartsCatalogService>();
builder.Services.AddScoped<Axpense.Service.Settings.IExpenseTypeService, Axpense.Service.Settings.ExpenseTypeService>();
builder.Services.AddScoped<Axpense.Service.Settings.ITaskCategoryService, Axpense.Service.Settings.TaskCategoryService>();
builder.Services.AddScoped<Axpense.Service.Settings.IPmEngineService, Axpense.Service.Settings.PmEngineService>();
builder.Services.AddScoped<Axpense.Service.Settings.IBudgetService, Axpense.Service.Settings.BudgetService>();
builder.Services.AddScoped<Axpense.Service.Settings.IPmTaskService, Axpense.Service.Settings.PmTaskService>();
builder.Services.AddScoped<Axpense.Service.Budgets.IBudgetLimitService, Axpense.Service.Budgets.BudgetLimitService>();
builder.Services.AddScoped<Axpense.Service.Reports.IReportService, Axpense.Service.Reports.ReportService>();
builder.Services.AddScoped<Axpense.Service.Company.ICompanyService, Axpense.Service.Company.CompanyService>();
builder.Services.AddScoped<Axpense.Service.Dashboard.IDashboardService, Axpense.Service.Dashboard.DashboardService>();

builder.Services.AddHealthChecks().AddDbContextCheck<AxpenseDbContext>();
builder.Services.AddAxpenseAuthentication(builder.Configuration);

var app = builder.Build();

// In production the app runs behind Caddy, which terminates TLS externally and talks to this
// container over plain HTTP on the internal Docker network. Without telling ASP.NET Core to
// trust Caddy's X-Forwarded-Proto/X-Forwarded-For headers, HttpContext.Request.Scheme still
// looks like "http" and app.UseHttpsRedirection() below would redirect every request back to
// https, causing a redirect loop against the proxy. Only Caddy can reach this container directly
// (it isn't published to the host), so trusting all forwarders here is safe.
var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaderOptions.KnownNetworks.Clear();
forwardedHeaderOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaderOptions);

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseRateLimiter();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AxpenseDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<TenantAuthorizationMiddleware>();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");
app.MapHealthChecks("/health");
app.MapGet("/ready", async (AxpenseDbContext db, CancellationToken ct) =>
{
    var ready = await db.Database.CanConnectAsync(ct);
    return ready ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
});
app.Run();
