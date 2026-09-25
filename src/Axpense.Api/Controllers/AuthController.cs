using System.Security.Claims;
using Axpense.Data.UserApplication;
using Axpense.Service.Auth;
using Axpense.Service.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(IAuthService authService, UserManager<ApplicationUser> userManager,
    Axpense.Infrastructure.Context.AxpenseDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted) return Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault();
        // The onboarding wizard is shown to owners/admins of a workspace whose setup was never finished or skipped.
        var settings = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.OrganizationSettings.Where(s => s.OrganizationId == user.OrganizationId));
        var onboardingRequired = role is "Owner" or "Admin" && settings is not null && settings.OnboardingCompletedAt is null;
        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            Role = role,
            OrganizationId = user.OrganizationId,
            OnboardingRequired = onboardingRequired
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await authService.UpdateProfileAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await authService.ChangePasswordAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous, HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await authService.ForgotPasswordAsync(request);
        // No email service yet: the reset token is only handed back in Development so the flow can be tested.
        if (!env.IsDevelopment()) result.ResetToken = null;
        return Ok(result);
    }

    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await authService.ResetPasswordAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}
