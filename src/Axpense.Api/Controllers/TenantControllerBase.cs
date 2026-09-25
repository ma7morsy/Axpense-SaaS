using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[ApiController]
public abstract class TenantControllerBase : ControllerBase
{
    protected Guid TenantId => Guid.TryParse(User.FindFirstValue("organization_id"), out var id) ? id : Guid.Empty;

    protected Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    /// <summary>Display name of the signed-in user, recorded on readings and history rows.</summary>
    protected string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "";
}
