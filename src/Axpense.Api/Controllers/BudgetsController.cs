using Axpense.Service.Budgets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Budgets page: monthly spending limits with actual spend. Changes need Owner or Admin.</summary>
[Route("api/budgets")]
public class BudgetsController(IBudgetLimitService budgets) : TenantControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) => Ok(await budgets.OptionsAsync(TenantId, ct));

    /// <summary>Budgets of a year (optionally one month) with actual spend, status and period totals.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        if (y is < 2000 or > 2100 || month is < 1 or > 12)
            return this.ToActionResult(Axpense.Service.Common.ServiceResult<bool>.Invalid(new Dictionary<string, string> { ["period"] = "Choose a valid year and month." }), _ => Ok());
        return Ok(await budgets.ListAsync(TenantId, y, month, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => this.ToActionResult(await budgets.GetAsync(TenantId, id, ct), Ok);

    [HttpPost, Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Create(BudgetRequest request, CancellationToken ct) =>
        this.ToActionResult(await budgets.CreateAsync(TenantId, CurrentUserId, request, ct), b => Created($"/api/budgets/{b.Id}", b));

    [HttpPut("{id:guid}"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Update(Guid id, BudgetRequest request, CancellationToken ct) =>
        this.ToActionResult(await budgets.UpdateAsync(TenantId, CurrentUserId, id, request, ct), Ok);

    [HttpDelete("{id:guid}"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await budgets.DeleteAsync(TenantId, id, ct), _ => NoContent());
}
