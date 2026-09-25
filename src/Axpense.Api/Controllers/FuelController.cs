using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Data.Entities.CostEntities;
using Axpense.Data.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[Route("api/fuel")]
public class FuelController(IMediator mediator) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid organizationId)
    {
        var list = await mediator.Send(new GetListwithFilterQuery<FuelTransaction>(x => x.OrganizationId == organizationId && x.CurrentState == (int)CurrentStatusType.Active));
        return Ok(list.OrderByDescending(x => x.TransactionDate));
    }

    [HttpPost]
    public async Task<IActionResult> Post(CreateFuelRequest r)
    {
        if (r.OrganizationId == Guid.Empty) return BadRequest("OrganizationId is required.");
        if (r.QuantityLiters <= 0 || r.UnitPrice <= 0) return BadRequest("Quantity and unit price must be greater than zero.");

        var vehicle = await mediator.Send(new GetFirstOrDefaultQuery<Vehicle>(v => v.Id == r.VehicleId && v.OrganizationId == r.OrganizationId));
        if (vehicle is null) return BadRequest("Vehicle does not belong to the organization.");

        var x = new FuelTransaction
        {
            OrganizationId = r.OrganizationId,
            VehicleId = r.VehicleId,
            TransactionDate = r.TransactionDate,
            QuantityLiters = r.QuantityLiters,
            UnitPrice = r.UnitPrice,
            TotalAmount = r.QuantityLiters * r.UnitPrice,
            Odometer = r.Odometer,
            FuelType = r.FuelType,
            Station = r.Station
        };
        var (success, id) = await mediator.Send(new AddAsyncGetIDCommand<FuelTransaction>(x, CurrentUserId));
        if (!success) return BadRequest("Could not create fuel transaction.");
        return Created($"/api/fuel/{id}", x);
    }
}

public record CreateFuelRequest(Guid OrganizationId, Guid VehicleId, DateTime TransactionDate, decimal QuantityLiters, decimal UnitPrice, decimal? Odometer, string? FuelType, string? Station);
