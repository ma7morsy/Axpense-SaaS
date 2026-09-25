using System.Text;
using Axpense.Service.Company;
using Axpense.Service.Common;
using Axpense.Service.Drivers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

/// <summary>Administration → Company: profile, logo, subscription and billing. Changes require the Owner or Admin role.</summary>
[Route("api/company")]
public class CompanyController(ICompanyService company) : TenantControllerBase
{
    private const long MaxUploadBytes = CompanyService.MaxLogoBytes + 64 * 1024;

    [HttpGet("options")]
    public IActionResult Options() => Ok(company.Options());

    // ---------------------------------------------------------------- profile
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct) => Ok(await company.GetProfileAsync(TenantId, ct));

    [HttpPut("profile"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> SaveProfile(CompanyProfileRequest request, CancellationToken ct) =>
        this.ToActionResult(await company.SaveProfileAsync(TenantId, CurrentUserId, request, ct), Ok);

    // ---------------------------------------------------------------- onboarding
    [HttpPost("onboarding"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> CompleteOnboarding(CompanyProfileRequest request, CancellationToken ct) =>
        this.ToActionResult(await company.CompleteOnboardingAsync(TenantId, CurrentUserId, request, ct), Ok);

    [HttpPost("onboarding/skip"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> SkipOnboarding(CancellationToken ct) =>
        this.ToActionResult(await company.CompleteOnboardingAsync(TenantId, CurrentUserId, null, ct), Ok);

    [HttpGet("logo")]
    public async Task<IActionResult> Logo(CancellationToken ct)
    {
        var result = await company.OpenLogoAsync(TenantId, ct);
        if (!result.Succeeded) return this.Error(result.Error!);
        // SVGs are checked on upload; the CSP is a second line of defence if the file is opened directly.
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; img-src data:";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.ContentType);
    }

    [HttpPost("logo"), Authorize(Roles = "Owner,Admin")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadLogo(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            return this.ToActionResult(ServiceResult<bool>.Invalid(new Dictionary<string, string> { ["logo"] = "Choose a logo to upload." }), _ => NoContent());
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return this.ToActionResult(await company.SetLogoAsync(TenantId, CurrentUserId, new FileUpload(buffer, file.FileName, file.ContentType, file.Length), ct), Ok);
    }

    [HttpDelete("logo"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> DeleteLogo(CancellationToken ct) =>
        this.ToActionResult(await company.DeleteLogoAsync(TenantId, CurrentUserId, ct), Ok);

    // ---------------------------------------------------------------- subscription
    [HttpGet("subscription")]
    public async Task<IActionResult> Subscription(CancellationToken ct) => Ok(await company.GetSubscriptionAsync(TenantId, CurrentUserId, ct));

    [HttpPost("subscription/plan"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> ChangePlan(ChangePlanRequest request, CancellationToken ct) =>
        this.ToActionResult(await company.ChangePlanAsync(TenantId, CurrentUserId, request, ct), Ok);

    [HttpPut("subscription/auto-renew"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> AutoRenew(AutoRenewRequest request, CancellationToken ct) =>
        this.ToActionResult(await company.SetAutoRenewAsync(TenantId, CurrentUserId, request.AutoRenew, ct), Ok);

    [HttpPost("subscription/cancel"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Cancel(CancellationToken ct) =>
        this.ToActionResult(await company.CancelAsync(TenantId, CurrentUserId, ct), Ok);

    [HttpPost("subscription/resume"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Resume(CancellationToken ct) =>
        this.ToActionResult(await company.ResumeAsync(TenantId, CurrentUserId, ct), Ok);

    // ---------------------------------------------------------------- billing
    [HttpGet("billing"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Billing(CancellationToken ct) => Ok(await company.GetBillingAsync(TenantId, CurrentUserId, ct));

    [HttpPut("billing/payment-method"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> PaymentMethod(PaymentMethodRequest request, CancellationToken ct) =>
        this.ToActionResult(await company.SetPaymentMethodAsync(TenantId, CurrentUserId, request, ct), Ok);

    [HttpPut("billing/email"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> BillingEmail(BillingEmailRequest request, CancellationToken ct) =>
        this.ToActionResult(await company.SetBillingEmailAsync(TenantId, CurrentUserId, request.Email, ct), Ok);

    [HttpGet("billing/invoices/{id:guid}/document"), Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Invoice(Guid id, CancellationToken ct)
    {
        var result = await company.InvoiceDocumentAsync(TenantId, id, ct);
        if (!result.Succeeded) return this.Error(result.Error!);
        return File(Encoding.UTF8.GetBytes(result.Value!.Html), "text/html; charset=utf-8", result.Value.FileName);
    }
}
