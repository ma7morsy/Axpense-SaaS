using Axpense.Service.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Aerial view: live positions and per-vehicle status. Tenant comes from the JWT.</summary>
[Route("api/tracking")]
public class TrackingController(IFleetTrackingService tracking) : TenantControllerBase
{
    [HttpGet("vehicles")]
    public async Task<IActionResult> Fleet(CancellationToken ct) =>
        Ok(await tracking.GetFleetAsync(TenantId, ct));

    [HttpGet("vehicles/{id:guid}")]
    public async Task<IActionResult> Vehicle(Guid id, CancellationToken ct) =>
        this.ToActionResult(await tracking.GetVehicleAsync(TenantId, id, ct), d => Ok(d));
}
