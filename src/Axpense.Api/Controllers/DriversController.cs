using Axpense.Service.Drivers;
using Axpense.Service.Drivers.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>
/// Drivers module. Thin controller: the tenant comes from the JWT (never from the request),
/// all rules live in <see cref="IDriverService"/>.
/// </summary>
[Route("api/drivers")]
public class DriversController(IDriverService drivers) : TenantControllerBase
{
    private const long MaxUploadBytes = 6 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DriverListQuery query, CancellationToken ct) =>
        Ok(await drivers.ListAsync(TenantId, query, ct));

    [HttpGet("next-employee-number")]
    public async Task<IActionResult> NextEmployeeNumber(CancellationToken ct) =>
        Ok(new { employeeNumber = await drivers.NextEmployeeNumberAsync(TenantId, ct) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        this.ToActionResult(await drivers.GetProfileAsync(TenantId, id, ct), p => Ok(p));

    [HttpPost]
    public async Task<IActionResult> Create(DriverUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await drivers.CreateAsync(TenantId, CurrentUserId, request, ct),
            profile => Created($"/api/drivers/{profile.Id}", profile));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DriverUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await drivers.UpdateAsync(TenantId, CurrentUserId, id, request, ct), p => Ok(p));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await drivers.DeleteAsync(TenantId, id, ct), _ => NoContent());

    // ---------------------------------------------------------------- documents

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> AddDocument(Guid id, [FromForm] string? name, [FromForm] DateTime? expiryDate, IFormFile? file, CancellationToken ct)
    {
        FileUpload? upload = null;
        MemoryStream? buffer = null;
        try
        {
            if (file is not null)
            {
                buffer = new MemoryStream();
                await file.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                upload = new FileUpload(buffer, file.FileName, file.ContentType, file.Length);
            }

            var request = new DriverDocumentCreateRequest { Name = name ?? "", ExpiryDate = expiryDate };
            var result = await drivers.AddDocumentAsync(TenantId, CurrentUserId, id, request, upload, ct);
            return this.ToActionResult(result, doc => Created($"/api/drivers/{id}/documents/{doc.Id}", doc));
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken ct) =>
        this.ToActionResult(await drivers.DeleteDocumentAsync(TenantId, id, documentId, ct), _ => NoContent());

    [HttpGet("{id:guid}/documents/{documentId:guid}/file")]
    public async Task<IActionResult> DocumentFile(Guid id, Guid documentId, CancellationToken ct) =>
        this.ToActionResult(await drivers.OpenDocumentFileAsync(TenantId, id, documentId, ct),
            f => File(f.Content, f.ContentType, f.FileName));
}
