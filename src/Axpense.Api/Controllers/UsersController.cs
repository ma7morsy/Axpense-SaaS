using Axpense.Api;
using Axpense.Data.UserApplication;
using Axpense.Service.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[Route("api/users")]
public class UsersController(IAuthService authService, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager,
    Axpense.Infrastructure.Context.AxpenseDbContext db) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var users = await authService.GetOrganizationUsersAsync(TenantId);
        return Ok(users);
    }

    [Authorize(Roles = "Owner,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest r)
    {
        if (r.OrganizationId != TenantId) return Forbid();
        if (string.IsNullOrWhiteSpace(r.Password)) return BadRequest("Password is required.");

        var email = r.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null) return Conflict("Email already exists.");
        var limit = await Axpense.Service.Company.PlanLimits.CheckAsync(db, TenantId, vehicle: false, HttpContext.RequestAborted);
        if (limit is not null) return this.Error(new Axpense.Service.Common.ServiceError(Axpense.Service.Common.ServiceErrorType.Conflict, "PLAN_LIMIT", limit));

        var nameParts = r.FullName.Trim().Split(' ', 2);
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            OrganizationId = TenantId,
            FirstName = nameParts[0],
            LastName = nameParts.Length > 1 ? nameParts[1] : ""
        };

        var result = await userManager.CreateAsync(user, r.Password);
        if (!result.Succeeded) return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        var role = string.IsNullOrWhiteSpace(r.Role) ? "Staff" : r.Role!;
        if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        await userManager.AddToRoleAsync(user, role);

        return Created($"/api/users/{user.Id}", new { user.Id, FullName = r.FullName, user.Email, Role = role, IsActive = true });
    }

    [Authorize(Roles = "Owner,Admin")]
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromBody] UserStatusRequest r)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.OrganizationId != TenantId) return NotFound();
        if (r.IsActive && user.IsDeleted)
        {
            var limit = await Axpense.Service.Company.PlanLimits.CheckAsync(db, TenantId, vehicle: false, HttpContext.RequestAborted);
            if (limit is not null) return this.Error(new Axpense.Service.Common.ServiceError(Axpense.Service.Common.ServiceErrorType.Conflict, "PLAN_LIMIT", limit));
        }

        user.IsDeleted = !r.IsActive;
        user.DeletedAt = r.IsActive ? null : DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return Ok(new { user.Id, IsActive = !user.IsDeleted });
    }
}

public record CreateUserRequest(Guid OrganizationId, string FullName, string Email, string Password, string? Role);
public record UserStatusRequest(bool IsActive);
