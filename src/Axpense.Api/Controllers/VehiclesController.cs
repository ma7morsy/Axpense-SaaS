using Axpense.Service.Drivers.Dtos;
using Axpense.Service.Vehicles;
using Axpense.Service.Vehicles.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>
/// Vehicles module. Thin controller: the tenant comes from the JWT (never from the request body or
/// query), all rules live in <see cref="IVehicleService"/> and <see cref="IVehicleRecordsService"/>.
/// </summary>
[Route("api/vehicles")]
public class VehiclesController(IVehicleService vehicles, IVehicleRecordsService records) : TenantControllerBase
{
    private const long MaxUploadBytes = 6 * 1024 * 1024;

    [HttpGet("options")]
    public IActionResult Options() => Ok(vehicles.Options());

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] VehicleListQuery query, CancellationToken ct) =>
        Ok(await vehicles.ListAsync(TenantId, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        this.ToActionResult(await vehicles.GetAsync(TenantId, id, ct), v => Ok(v));

    [HttpPost]
    public async Task<IActionResult> Create(VehicleUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await vehicles.CreateAsync(TenantId, CurrentUserId, CurrentUserName, request, ct), v => Created($"/api/vehicles/{v.Id}", v));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, VehicleUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await vehicles.UpdateAsync(TenantId, CurrentUserId, CurrentUserName, id, request, ct), v => Ok(v));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.ToActionResult(await vehicles.DeleteAsync(TenantId, id, ct), _ => NoContent());

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, VehicleStatusRequest request, CancellationToken ct) =>
        this.ToActionResult(await vehicles.SetStatusAsync(TenantId, CurrentUserId, id, request, ct), v => Ok(v));

    [HttpPost("{id:guid}/handoff")]
    public async Task<IActionResult> HandOff(Guid id, VehicleHandOffRequest request, CancellationToken ct) =>
        this.ToActionResult(await vehicles.HandOffAsync(TenantId, CurrentUserId, CurrentUserName, id, request, ct), v => Ok(v));

    // ---------------------------------------------------------------- photo

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct) =>
        this.ToActionResult(await vehicles.OpenPhotoAsync(TenantId, id, ct), f => File(f.Content, f.ContentType));

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            return this.ToActionResult(Axpense.Service.Common.ServiceResult<bool>.Invalid(new Dictionary<string, string> { ["photo"] = "Choose a photo to upload." }), _ => NoContent());
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var result = await vehicles.SetPhotoAsync(TenantId, CurrentUserId, id, new FileUpload(buffer, file.FileName, file.ContentType, file.Length), ct);
        return this.ToActionResult(result, _ => NoContent());
    }

    [HttpDelete("{id:guid}/photo")]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken ct) =>
        this.ToActionResult(await vehicles.DeletePhotoAsync(TenantId, CurrentUserId, id, ct), _ => NoContent());

    // ---------------------------------------------------------------- parts

    [HttpGet("{id:guid}/parts")]
    public async Task<IActionResult> Parts(Guid id, CancellationToken ct) =>
        this.ToActionResult(await records.PartsAsync(TenantId, id, ct), x => Ok(x));

    [HttpPost("{id:guid}/parts")]
    public async Task<IActionResult> AddPart(Guid id, VehiclePartRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.AddPartAsync(TenantId, CurrentUserId, id, request, ct), x => Created($"/api/vehicles/{id}/parts/{x.Id}", x));

    [HttpPut("{id:guid}/parts/{partId:guid}")]
    public async Task<IActionResult> UpdatePart(Guid id, Guid partId, VehiclePartRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.UpdatePartAsync(TenantId, CurrentUserId, id, partId, request, ct), x => Ok(x));

    [HttpDelete("{id:guid}/parts/{partId:guid}")]
    public async Task<IActionResult> DeletePart(Guid id, Guid partId, CancellationToken ct) =>
        this.ToActionResult(await records.DeletePartAsync(TenantId, id, partId, ct), _ => NoContent());

    [HttpPost("{id:guid}/parts/{partId:guid}/replace")]
    public async Task<IActionResult> ReplacePart(Guid id, Guid partId, VehiclePartReplaceRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.ReplacePartAsync(TenantId, CurrentUserId, id, partId, request, ct), x => Ok(x));

    // ---------------------------------------------------------------- issues

    [HttpGet("{id:guid}/issues")]
    public async Task<IActionResult> Issues(Guid id, CancellationToken ct) =>
        this.ToActionResult(await records.IssuesAsync(TenantId, id, ct), x => Ok(x));

    [HttpPost("{id:guid}/issues")]
    public async Task<IActionResult> AddIssue(Guid id, VehicleIssueRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.AddIssueAsync(TenantId, CurrentUserId, id, request, ct), x => Created($"/api/vehicles/{id}/issues/{x.Id}", x));

    [HttpPut("{id:guid}/issues/{issueId:guid}/status")]
    public async Task<IActionResult> SetIssueStatus(Guid id, Guid issueId, VehicleIssueStatusRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.SetIssueStatusAsync(TenantId, CurrentUserId, id, issueId, request, ct), x => Ok(x));

    [HttpDelete("{id:guid}/issues/{issueId:guid}")]
    public async Task<IActionResult> DeleteIssue(Guid id, Guid issueId, CancellationToken ct) =>
        this.ToActionResult(await records.DeleteIssueAsync(TenantId, id, issueId, ct), _ => NoContent());

    // ---------------------------------------------------------------- fuel

    [HttpGet("{id:guid}/fuel")]
    public async Task<IActionResult> Fuel(Guid id, CancellationToken ct) =>
        this.ToActionResult(await records.FuelAsync(TenantId, id, ct), x => Ok(x));

    [HttpPost("{id:guid}/fuel")]
    public async Task<IActionResult> AddFuel(Guid id, FuelRecordRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.AddFuelAsync(TenantId, CurrentUserId, CurrentUserName, id, request, ct), x => Created($"/api/vehicles/{id}/fuel/{x.Id}", x));

    [HttpDelete("{id:guid}/fuel/{fuelId:guid}")]
    public async Task<IActionResult> DeleteFuel(Guid id, Guid fuelId, CancellationToken ct) =>
        this.ToActionResult(await records.DeleteFuelAsync(TenantId, id, fuelId, ct), _ => NoContent());

    // ---------------------------------------------------------------- odometer

    [HttpGet("{id:guid}/readings")]
    public async Task<IActionResult> Readings(Guid id, CancellationToken ct) =>
        this.ToActionResult(await records.ReadingsAsync(TenantId, id, ct), x => Ok(x));

    [HttpPost("{id:guid}/readings")]
    public async Task<IActionResult> AddReading(Guid id, OdometerReadingRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.AddReadingAsync(TenantId, CurrentUserId, CurrentUserName, id, request, ct), x => Created($"/api/vehicles/{id}/readings/{x.Id}", x));

    [HttpDelete("{id:guid}/readings/{readingId:guid}")]
    public async Task<IActionResult> DeleteReading(Guid id, Guid readingId, CancellationToken ct) =>
        this.ToActionResult(await records.DeleteReadingAsync(TenantId, CurrentUserId, id, readingId, ct), _ => NoContent());

    // ---------------------------------------------------------------- expenses

    [HttpGet("{id:guid}/expenses")]
    public async Task<IActionResult> Expenses(Guid id, CancellationToken ct) =>
        this.ToActionResult(await records.ExpensesAsync(TenantId, id, ct), x => Ok(x));

    [HttpPost("{id:guid}/expenses")]
    public async Task<IActionResult> AddExpense(Guid id, VehicleExpenseRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.AddExpenseAsync(TenantId, CurrentUserId, id, request, ct), x => Created($"/api/vehicles/{id}/expenses/{x.Id}", x));

    [HttpPut("{id:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> UpdateExpense(Guid id, Guid expenseId, VehicleExpenseRequest request, CancellationToken ct) =>
        this.ToActionResult(await records.UpdateExpenseAsync(TenantId, CurrentUserId, id, expenseId, request, ct), x => Ok(x));

    [HttpDelete("{id:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, Guid expenseId, CancellationToken ct) =>
        this.ToActionResult(await records.DeleteExpenseAsync(TenantId, id, expenseId, ct), _ => NoContent());

    // ---------------------------------------------------------------- history

    [HttpGet("{id:guid}/inspections")]
    public async Task<IActionResult> Inspections(Guid id, CancellationToken ct) =>
        this.ToActionResult(await records.InspectionsAsync(TenantId, id, ct), x => Ok(x));

    [HttpGet("{id:guid}/assignments")]
    public async Task<IActionResult> Assignments(Guid id, CancellationToken ct) =>
        this.ToActionResult(await records.AssignmentsAsync(TenantId, id, ct), x => Ok(x));
}
