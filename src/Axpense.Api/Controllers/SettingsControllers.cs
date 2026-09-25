using Axpense.Service.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Settings → Expense types. Read: everyone in the organization; write: Owner/Admin.</summary>
[Route("api/settings/expense-types")]
public class ExpenseTypesController(IExpenseTypeService types) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await types.ListAsync(TenantId, ct));

    [HttpPost, Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Create(ExpenseTypeRequest request, CancellationToken ct) =>
        this.ToActionResult(await types.CreateAsync(TenantId, CurrentUserId, request, ct), t => Created($"/api/settings/expense-types/{t.Id}", t));

    [HttpPut("{id:guid}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Update(Guid id, ExpenseTypeRequest request, CancellationToken ct) =>
        this.ToActionResult(await types.UpdateAsync(TenantId, CurrentUserId, id, request, ct), t => Ok(t));

    [HttpDelete("{id:guid}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await types.DeleteAsync(TenantId, id, ct), _ => NoContent());
}

/// <summary>Settings → Maintenance → Task categories.</summary>
[Route("api/settings/task-categories")]
public class TaskCategoriesController(ITaskCategoryService categories) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await categories.ListAsync(TenantId, ct));

    [HttpPost, Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Create(TaskCategoryRequest request, CancellationToken ct) =>
        this.ToActionResult(await categories.CreateAsync(TenantId, CurrentUserId, request, ct), c => Created($"/api/settings/task-categories/{c.Id}", c));

    [HttpPut("{id:guid}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Update(Guid id, TaskCategoryRequest request, CancellationToken ct) =>
        this.ToActionResult(await categories.UpdateAsync(TenantId, CurrentUserId, id, request, ct), c => Ok(c));

    [HttpDelete("{id:guid}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await categories.DeleteAsync(TenantId, id, ct), _ => NoContent());
}

/// <summary>Settings → Maintenance → PM engine rules.</summary>
[Route("api/settings/pm-engine")]
public class PmEngineController(IPmEngineService engine) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await engine.GetAsync(TenantId, ct));

    [HttpPut, Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Update(PmEngineRequest request, CancellationToken ct) =>
        this.ToActionResult(await engine.UpdateAsync(TenantId, CurrentUserId, request, ct), d => Ok(d));
}

/// <summary>Settings → Budget (annual amount, monthly plan, category allocation).</summary>
[Route("api/settings/budget/{year:int}")]
public class BudgetSettingsController(IBudgetService budgets) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int year, CancellationToken ct) => Ok(await budgets.GetAsync(TenantId, year, ct));

    [HttpPut("annual"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> SetAnnual(int year, AmountRequest request, CancellationToken ct) =>
        this.ToActionResult(await budgets.SetAnnualAsync(TenantId, CurrentUserId, year, request.Amount, ct), b => Ok(b));

    [HttpPut("months/{month:int}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> SetMonth(int year, int month, AmountRequest request, CancellationToken ct) =>
        this.ToActionResult(await budgets.SetMonthAsync(TenantId, CurrentUserId, year, month, request.Amount, ct), b => Ok(b));

    [HttpPost("months/split-evenly"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> SplitEvenly(int year, CancellationToken ct) =>
        this.ToActionResult(await budgets.SplitEvenlyAsync(TenantId, CurrentUserId, year, ct), b => Ok(b));

    [HttpPost("months/weight-by-last-year"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> WeightByLastYear(int year, CancellationToken ct) =>
        this.ToActionResult(await budgets.WeightByLastYearAsync(TenantId, CurrentUserId, year, ct), b => Ok(b));

    [HttpPut("categories/{expenseTypeId:guid}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> SetShare(int year, Guid expenseTypeId, ShareRequest request, CancellationToken ct) =>
        this.ToActionResult(await budgets.SetShareAsync(TenantId, CurrentUserId, year, expenseTypeId, request.SharePercent, ct), b => Ok(b));

    [HttpPost("categories/distribute-from-last-year"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Distribute(int year, CancellationToken ct) =>
        this.ToActionResult(await budgets.DistributeFromLastYearAsync(TenantId, CurrentUserId, year, ct), b => Ok(b));
}

internal static class SettingsRoles
{
    public const string Admins = "Owner,Admin";
}

/// <summary>Settings → Maintenance → PM task library.</summary>
[Route("api/settings/pm-tasks")]
public class PmTasksController(IPmTaskService tasks) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await tasks.ListAsync(TenantId, ct));

    [HttpPost, Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Create(PmTaskRequest request, CancellationToken ct) =>
        this.ToActionResult(await tasks.CreateAsync(TenantId, CurrentUserId, request, ct), t => Created($"/api/settings/pm-tasks/{t.Id}", t));

    [HttpPut("{id:guid}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Update(Guid id, PmTaskRequest request, CancellationToken ct) =>
        this.ToActionResult(await tasks.UpdateAsync(TenantId, CurrentUserId, id, request, ct), t => Ok(t));

    [HttpDelete("{id:guid}"), Authorize(Roles = SettingsRoles.Admins)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await tasks.DeleteAsync(TenantId, id, ct), _ => NoContent());
}
