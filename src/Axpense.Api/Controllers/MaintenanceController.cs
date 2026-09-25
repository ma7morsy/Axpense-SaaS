using Axpense.Service.WorkOrders;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Maintenance module — work orders. Thin controller: tenant from the JWT, rules in <see cref="IWorkOrderService"/>.</summary>
[Route("api/work-orders")]
public class WorkOrdersController(IWorkOrderService workOrders) : TenantControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) => Ok(await workOrders.OptionsAsync(TenantId, ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] WorkOrderListQuery query, CancellationToken ct) => Ok(await workOrders.ListAsync(TenantId, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        this.ToActionResult(await workOrders.GetAsync(TenantId, id, ct), x => Ok(x));

    [HttpPost]
    public async Task<IActionResult> Create(WorkOrderRequest request, CancellationToken ct) =>
        this.ToActionResult(await workOrders.CreateAsync(TenantId, CurrentUserId, CurrentUserName, request, ct), x => Created($"/api/work-orders/{x.Id}", x));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, WorkOrderRequest request, CancellationToken ct) =>
        this.ToActionResult(await workOrders.UpdateAsync(TenantId, CurrentUserId, CurrentUserName, id, request, ct), x => Ok(x));

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, WorkOrderStatusRequest request, CancellationToken ct) =>
        this.ToActionResult(await workOrders.SetStatusAsync(TenantId, CurrentUserId, CurrentUserName, id, request, ct), x => Ok(x));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await workOrders.DeleteAsync(TenantId, id, ct), _ => NoContent());

    /// <summary>Raises preventive work orders for vehicles whose PM is due or overdue.</summary>
    [HttpGet("pm-engine/preview")]
    public async Task<IActionResult> PreviewPmEngine(CancellationToken ct) => Ok(await workOrders.PreviewPmEngineAsync(TenantId, ct));

    [HttpPost("run-pm-engine")]
    public async Task<IActionResult> RunPmEngine([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] PmEngineRunRequest? request, CancellationToken ct) =>
        Ok(await workOrders.RunPmEngineAsync(TenantId, CurrentUserId, request?.VehicleIds, ct));
}

/// <summary>Fleet-wide issue list (the per-vehicle actions live under /api/vehicles/{id}/issues).</summary>
[Route("api/issues")]
public class IssuesController(IWorkOrderService workOrders) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) => Ok(await workOrders.IssuesAsync(TenantId, status, ct));
}
