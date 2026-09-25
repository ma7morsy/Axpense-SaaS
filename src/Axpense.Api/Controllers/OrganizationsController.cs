using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Data.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[Route("api/organizations")]
public class OrganizationsController(IMediator mediator) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var list = await mediator.Send(new GetListwithFilterQuery<Organization>(x => x.CurrentState == (int)CurrentStatusType.Active));
        return Ok(list.OrderBy(x => x.Name));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest("Organization name is required.");

        var o = new Organization { Name = r.Name.Trim() };
        var (success, id) = await mediator.Send(new AddAsyncGetIDCommand<Organization>(o, CurrentUserId));
        if (!success) return BadRequest("Could not create organization.");
        return Created($"/api/organizations/{id}", o);
    }
}

public record CreateOrganizationRequest(string Name);
