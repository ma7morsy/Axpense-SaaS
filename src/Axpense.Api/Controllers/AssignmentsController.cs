using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Data.Entities.DriverEntities;
using Axpense.Data.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[Route("api/assignments")]
public class AssignmentsController(IMediator mediator) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid organizationId)
    {
        var assignments = await mediator.Send(new GetListwithFilterQuery<VehicleAssignment>(x => x.OrganizationId == organizationId && x.CurrentState == (int)CurrentStatusType.Active));
        var vehicles = await mediator.Send(new GetListwithFilterQuery<Vehicle>(x => x.OrganizationId == organizationId));
        var drivers = await mediator.Send(new GetListwithFilterQuery<Driver>(x => x.OrganizationId == organizationId));
        var plateByVehicleId = vehicles.ToDictionary(v => v.Id, v => v.PlateNumber);
        var nameByDriverId = drivers.ToDictionary(d => d.Id, d => d.FullName);

        var result = assignments.OrderByDescending(x => x.StartDate).Select(x => new
        {
            x.Id,
            x.VehicleId,
            x.DriverId,
            x.StartDate,
            x.EndDate,
            x.AssignmentType,
            x.Notes,
            Vehicle = plateByVehicleId.GetValueOrDefault(x.VehicleId),
            Driver = nameByDriverId.GetValueOrDefault(x.DriverId)
        });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Post(CreateAssignmentRequest r)
    {
        if (r.OrganizationId == Guid.Empty) return BadRequest();

        var vehicleOk = await mediator.Send(new GetFirstOrDefaultQuery<Vehicle>(v => v.Id == r.VehicleId && v.OrganizationId == r.OrganizationId));
        var driverOk = await mediator.Send(new GetFirstOrDefaultQuery<Driver>(d => d.Id == r.DriverId && d.OrganizationId == r.OrganizationId));
        if (vehicleOk is null || driverOk is null) return BadRequest("Vehicle or driver does not belong to the organization.");

        var a = new VehicleAssignment
        {
            OrganizationId = r.OrganizationId,
            VehicleId = r.VehicleId,
            DriverId = r.DriverId,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            AssignmentType = r.AssignmentType ?? "Primary",
            Notes = r.Notes
        };

        var (success, id) = await mediator.Send(new AddAsyncGetIDCommand<VehicleAssignment>(a, CurrentUserId));
        if (!success) return BadRequest("Could not create assignment.");
        return Created($"/api/assignments/{id}", a);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, UpdateAssignmentRequest input)
    {
        var a = await mediator.Send(new GetEntityByIdQuery<VehicleAssignment>(id));
        if (a is null || a.OrganizationId != input.OrganizationId) return NotFound();

        a.VehicleId = input.VehicleId;
        a.DriverId = input.DriverId;
        a.StartDate = input.StartDate;
        a.EndDate = input.EndDate;
        a.AssignmentType = input.AssignmentType;
        a.Notes = input.Notes;

        var affected = await mediator.Send(new UpdateCommand<VehicleAssignment>(a, CurrentUserId));
        return affected <= 0 ? NotFound() : Ok(a);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, Guid organizationId)
    {
        var a = await mediator.Send(new GetEntityByIdQuery<VehicleAssignment>(id));
        if (a is null || a.OrganizationId != organizationId) return NotFound();

        var ok = await mediator.Send(new DeleteCommand<VehicleAssignment>(id));
        return ok ? NoContent() : NotFound();
    }
}

public record CreateAssignmentRequest(Guid OrganizationId, Guid VehicleId, Guid DriverId, DateTime StartDate, DateTime? EndDate, string? AssignmentType, string? Notes);
public record UpdateAssignmentRequest(Guid OrganizationId, Guid VehicleId, Guid DriverId, DateTime StartDate, DateTime? EndDate, string AssignmentType, string? Notes);
