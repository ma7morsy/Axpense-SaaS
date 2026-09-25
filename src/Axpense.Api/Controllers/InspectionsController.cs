using Axpense.Service.Drivers.Dtos;
using Axpense.Service.Inspections;
using Axpense.Service.Inspections.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Inspection runs. Thin controller: tenant from the JWT, rules in <see cref="IInspectionService"/>.</summary>
[Route("api/inspections")]
public class InspectionsController(IInspectionService inspections) : TenantControllerBase
{
    private const long MaxUploadBytes = 6 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] InspectionListQuery query, CancellationToken ct) =>
        Ok(await inspections.ListAsync(TenantId, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        this.ToActionResult(await inspections.GetAsync(TenantId, id, ct), x => Ok(x));

    /// <summary>Starts a run (a server-side draft) from a template.</summary>
    [HttpPost]
    public async Task<IActionResult> Start(InspectionStartRequest request, CancellationToken ct) =>
        this.ToActionResult(await inspections.StartAsync(TenantId, CurrentUserId, request, ct), x => Created($"/api/inspections/{x.Id}", x));

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> Answer(Guid id, Guid itemId, InspectionAnswerRequest request, CancellationToken ct) =>
        this.ToActionResult(await inspections.AnswerAsync(TenantId, CurrentUserId, id, itemId, request, ct), x => Ok(x));

    [HttpPost("{id:guid}/items/{itemId:guid}/photo")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadPhoto(Guid id, Guid itemId, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            return this.ToActionResult(Axpense.Service.Common.ServiceResult<bool>.Invalid(new Dictionary<string, string> { ["photo"] = "Choose a photo to upload." }), _ => NoContent());
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var result = await inspections.SetPhotoAsync(TenantId, CurrentUserId, id, itemId, new FileUpload(buffer, file.FileName, file.ContentType, file.Length), ct);
        return this.ToActionResult(result, x => Ok(x));
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}/photo")]
    public async Task<IActionResult> RemovePhoto(Guid id, Guid itemId, CancellationToken ct) =>
        this.ToActionResult(await inspections.RemovePhotoAsync(TenantId, CurrentUserId, id, itemId, ct), x => Ok(x));

    [HttpGet("{id:guid}/items/{itemId:guid}/photo")]
    public async Task<IActionResult> Photo(Guid id, Guid itemId, CancellationToken ct) =>
        this.ToActionResult(await inspections.OpenPhotoAsync(TenantId, id, itemId, ct), f => File(f.Content, f.ContentType));

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await inspections.CompleteAsync(TenantId, CurrentUserId, id, ct), x => Ok(x));

    /// <summary>Discards a draft or deletes a completed inspection (issues it raised stay).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await inspections.DeleteAsync(TenantId, id, ct), _ => NoContent());
}

[Route("api/inspection-templates")]
public class InspectionTemplatesController(IInspectionTemplateService templates) : TenantControllerBase
{
    [HttpGet("options")]
    public IActionResult Options() => Ok(templates.Options());

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await templates.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        this.ToActionResult(await templates.GetAsync(TenantId, id, ct), x => Ok(x));

    [HttpPost]
    public async Task<IActionResult> Create(TemplateUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await templates.CreateAsync(TenantId, CurrentUserId, request, ct), x => Created($"/api/inspection-templates/{x.Id}", x));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, TemplateUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await templates.UpdateAsync(TenantId, CurrentUserId, id, request, ct), x => Ok(x));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await templates.DeleteAsync(TenantId, id, ct), _ => NoContent());
}
