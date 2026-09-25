using Axpense.Service.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Reports module (read-only). Windowed reports accept days = 30 | 90 | 180 | 365 (default 90).</summary>
[Route("api/reports")]
public class ReportsController(IReportService reports) : TenantControllerBase
{
    [HttpGet("fuel")]
    public async Task<IActionResult> Fuel([FromQuery] int days = 90, [FromQuery] Guid? vehicleId = null, CancellationToken ct = default) =>
        this.ToActionResult(await reports.FuelAsync(TenantId, days, vehicleId, ct), Ok);

    [HttpGet("budget")]
    public async Task<IActionResult> Budget([FromQuery] int? year, CancellationToken ct) =>
        this.ToActionResult(await reports.BudgetAsync(TenantId, year ?? DateTime.UtcNow.Year, ct), Ok);

    [HttpGet("pm")]
    public async Task<IActionResult> Pm(CancellationToken ct) => Ok(await reports.PmComplianceAsync(TenantId, ct));

    [HttpGet("work-orders")]
    public async Task<IActionResult> WorkOrders(CancellationToken ct) => Ok(await reports.WorkOrdersAsync(TenantId, ct));

    [HttpGet("cost")]
    public async Task<IActionResult> Cost([FromQuery] int days = 90, CancellationToken ct = default) =>
        this.ToActionResult(await reports.CostAsync(TenantId, days, ct), Ok);

    [HttpGet("issues")]
    public async Task<IActionResult> Issues(CancellationToken ct) => Ok(await reports.IssuesAsync(TenantId, ct));

    [HttpGet("uptime")]
    public async Task<IActionResult> Uptime([FromQuery] int days = 90, CancellationToken ct = default) =>
        this.ToActionResult(await reports.UptimeAsync(TenantId, days, ct), Ok);

    [HttpGet("drivers")]
    public async Task<IActionResult> Drivers([FromQuery] int days = 90, CancellationToken ct = default) =>
        this.ToActionResult(await reports.DriversAsync(TenantId, days, ct), Ok);

    [HttpGet("odometer")]
    public async Task<IActionResult> Odometer([FromQuery] int days = 90, CancellationToken ct = default) =>
        this.ToActionResult(await reports.OdometerAsync(TenantId, days, ct), Ok);

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses(CancellationToken ct) => Ok(await reports.ExpensesAsync(TenantId, ct));
}
