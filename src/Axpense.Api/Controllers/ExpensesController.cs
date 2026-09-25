using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities.CostEntities;
using Axpense.Data.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[Route("api/expenses")]
public class ExpensesController(IMediator mediator) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid organizationId, DateTime? from = null, DateTime? to = null)
    {
        var list = await mediator.Send(new GetListwithFilterQuery<Expense>(x => x.OrganizationId == organizationId && x.CurrentState == (int)CurrentStatusType.Active));
        IEnumerable<Expense> q = list;
        if (from.HasValue) q = q.Where(x => x.ExpenseDate >= from);
        if (to.HasValue) q = q.Where(x => x.ExpenseDate <= to);
        return Ok(q.OrderByDescending(x => x.ExpenseDate));
    }

    [HttpPost]
    public async Task<IActionResult> Post(Expense x)
    {
        if (x.OrganizationId == Guid.Empty) return BadRequest("OrganizationId is required.");
        if (x.Amount <= 0) return BadRequest("Amount must be greater than zero.");
        if (x.VehicleId.HasValue)
        {
            var vehicle = await mediator.Send(new GetFirstOrDefaultQuery<Axpense.Data.Entities.Vehicle>(v => v.Id == x.VehicleId && v.OrganizationId == x.OrganizationId));
            if (vehicle is null) return BadRequest("Vehicle does not belong to the organization.");
        }

        var (success, id) = await mediator.Send(new AddAsyncGetIDCommand<Expense>(x, CurrentUserId));
        if (!success) return BadRequest("Could not create expense.");
        return Created($"/api/expenses/{id}", x);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, Guid organizationId)
    {
        var x = await mediator.Send(new GetEntityByIdQuery<Expense>(id));
        if (x is null || x.OrganizationId != organizationId) return NotFound();

        var ok = await mediator.Send(new DeleteCommand<Expense>(id));
        return ok ? NoContent() : NotFound();
    }
}
