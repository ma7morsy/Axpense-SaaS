using Axpense.Service.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Settings → Parts catalogue. Everyone in the organization can read; only Owner/Admin can change it.</summary>
[Route("api/settings/parts-catalogue")]
public class PartsCatalogController(IPartsCatalogService catalog) : TenantControllerBase
{
    private const string Admins = "Owner,Admin";

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await catalog.GetAsync(TenantId, ct));

    [HttpPost("categories"), Authorize(Roles = Admins)]
    public async Task<IActionResult> CreateCategory(CatalogNameRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.CreateCategoryAsync(TenantId, CurrentUserId, request, ct), c => Created($"/api/settings/parts-catalogue/categories/{c.Id}", c));

    [HttpPut("categories/{id:guid}"), Authorize(Roles = Admins)]
    public async Task<IActionResult> UpdateCategory(Guid id, CatalogNameRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.UpdateCategoryAsync(TenantId, CurrentUserId, id, request, ct), c => Ok(c));

    [HttpDelete("categories/{id:guid}"), Authorize(Roles = Admins)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct) =>
        this.ToActionResult(await catalog.DeleteCategoryAsync(TenantId, id, ct), _ => NoContent());

    [HttpPost("categories/{id:guid}/parts"), Authorize(Roles = Admins)]
    public async Task<IActionResult> CreatePart(Guid id, CatalogNameRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.CreatePartAsync(TenantId, CurrentUserId, id, request, ct), p => Created($"/api/settings/parts-catalogue/parts/{p.Id}", p));

    [HttpPut("parts/{id:guid}"), Authorize(Roles = Admins)]
    public async Task<IActionResult> UpdatePart(Guid id, CatalogNameRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.UpdatePartAsync(TenantId, CurrentUserId, id, request, ct), p => Ok(p));

    [HttpDelete("parts/{id:guid}"), Authorize(Roles = Admins)]
    public async Task<IActionResult> DeletePart(Guid id, CancellationToken ct) =>
        this.ToActionResult(await catalog.DeletePartAsync(TenantId, id, ct), _ => NoContent());
}
