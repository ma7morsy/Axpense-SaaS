using Axpense.Service.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Dashboard figures for the signed-in organization. Business rules live in <see cref="DashboardService"/>.</summary>
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : TenantControllerBase
{
    /// <summary>KPIs, cost breakdown, upcoming maintenance and recent expenses for an inclusive date range (default: last month).</summary>
    [HttpGet]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        this.ToActionResult(await dashboard.SummaryAsync(TenantId, from, to, ct), Ok);

    [HttpGet("cost-trend")]
    public async Task<IActionResult> CostTrend([FromQuery] int months = 6, CancellationToken ct = default) =>
        this.ToActionResult(await dashboard.CostTrendAsync(TenantId, months, ct), Ok);

    [HttpGet("operating-trend")]
    public async Task<IActionResult> OperatingTrend([FromQuery] string? granularity, CancellationToken ct) =>
        this.ToActionResult(await dashboard.OperatingTrendAsync(TenantId, granularity, ct), Ok);

    [HttpGet("budget")]
    public async Task<IActionResult> Budget([FromQuery] string? groupBy, CancellationToken ct) =>
        this.ToActionResult(await dashboard.BudgetAsync(TenantId, groupBy, ct), Ok);

    [HttpGet("recurrence")]
    public async Task<IActionResult> Recurrence([FromQuery] int take = 7, CancellationToken ct = default) =>
        this.ToActionResult(await dashboard.RecurrenceAsync(TenantId, take, ct), Ok);
}
