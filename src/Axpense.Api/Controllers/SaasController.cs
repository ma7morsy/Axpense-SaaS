using System.Text;
using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Data.Entities.SaasEntities;
using Axpense.Data.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[Route("api/saas")]
public class SaasController(IMediator mediator) : TenantControllerBase
{
    [HttpGet("audit")]
    public async Task<IActionResult> AuditLogs([FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var list = await mediator.Send(new GetListwithFilterQuery<AuditLog>(x => x.OrganizationId == TenantId));
        return Ok(list.OrderByDescending(x => x.CreatedAt).Take(take));
    }

    [HttpPost("audit")]
    public async Task<IActionResult> AddAudit(AuditLog input)
    {
        input.OrganizationId = TenantId;
        input.UserId = CurrentUserId;
        var (success, id) = await mediator.Send(new AddAsyncGetIDCommand<AuditLog>(input, CurrentUserId));
        if (!success) return BadRequest("Could not create audit log.");
        return Ok(input);
    }

    [HttpGet("export/vehicles.csv")]
    public async Task<IActionResult> ExportVehicles()
    {
        var rows = await mediator.Send(new GetListwithFilterQuery<Vehicle>(x => x.OrganizationId == TenantId && x.CurrentState == (int)CurrentStatusType.Active));
        var sb = new StringBuilder("PlateNumber,Make,Model,Year,VIN,Status,Odometer\n");
        foreach (var x in rows)
            sb.AppendLine($"{Esc(x.PlateNumber)},{Esc(x.Make)},{Esc(x.Model)},{x.ModelYear},{Esc(x.Vin)},{Esc(x.Status)},{x.CurrentOdometer}");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "axpense-vehicles.csv");
    }

    static string Esc(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    async Task Audit(string action, string entity, string? id)
    {
        var log = new AuditLog { OrganizationId = TenantId, UserId = CurrentUserId, Action = action, EntityType = entity, EntityId = id };
        await mediator.Send(new AddAsyncGetIDCommand<AuditLog>(log, CurrentUserId));
    }
}
