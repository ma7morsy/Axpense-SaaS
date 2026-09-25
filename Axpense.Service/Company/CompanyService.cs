using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Axpense.Data.Constants;
using Axpense.Data.Entities.SaasEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Service.Common;
using Axpense.Service.Drivers.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Company
{
    public interface ICompanyService
    {
        CompanyOptionsDto Options();
        Task<CompanyProfileDto> GetProfileAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<CompanyProfileDto>> SaveProfileAsync(Guid organizationId, Guid userId, CompanyProfileRequest request, CancellationToken ct = default);
        Task<ServiceResult<CompanyProfileDto>> CompleteOnboardingAsync(Guid organizationId, Guid userId, CompanyProfileRequest? request, CancellationToken ct = default);
        Task<ServiceResult<CompanyProfileDto>> SetLogoAsync(Guid organizationId, Guid userId, FileUpload file, CancellationToken ct = default);
        Task<ServiceResult<CompanyProfileDto>> DeleteLogoAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult<FileDownload>> OpenLogoAsync(Guid organizationId, CancellationToken ct = default);

        Task<SubscriptionDto> GetSubscriptionAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult<SubscriptionDto>> ChangePlanAsync(Guid organizationId, Guid userId, ChangePlanRequest request, CancellationToken ct = default);
        Task<ServiceResult<SubscriptionDto>> SetAutoRenewAsync(Guid organizationId, Guid userId, bool autoRenew, CancellationToken ct = default);
        Task<ServiceResult<SubscriptionDto>> CancelAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult<SubscriptionDto>> ResumeAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

        Task<BillingDto> GetBillingAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult<BillingDto>> SetPaymentMethodAsync(Guid organizationId, Guid userId, PaymentMethodRequest request, CancellationToken ct = default);
        Task<ServiceResult<BillingDto>> SetBillingEmailAsync(Guid organizationId, Guid userId, string? email, CancellationToken ct = default);
        Task<ServiceResult<InvoiceDocument>> InvoiceDocumentAsync(Guid organizationId, Guid invoiceId, CancellationToken ct = default);
    }

    /// <summary>
    /// Administration → Company: profile, logo, subscription and billing. Rules:
    /// <list type="bullet">
    /// <item>No payment gateway is integrated. The API never receives a card number or CVC — only brand, last 4, expiry and holder.
    /// New invoices are "Issued" (awaiting the provider); only a provider callback (not built yet) should mark them "Paid".</item>
    /// <item>Renewal is processed when the subscription is read: each elapsed period with auto-renew on rolls forward and issues an invoice.
    /// A period that ends with auto-renew off, or while "Cancelling", ends the subscription ("Cancelled").</item>
    /// <item>Plan changes take effect immediately, start a new period and issue a full-price invoice (no proration).
    /// A change is refused when current usage exceeds the new plan's limits.</item>
    /// <item>Plan limits (vehicles, seats) are enforced on vehicle and user creation through <see cref="PlanLimits"/>.</item>
    /// </list>
    /// </summary>
    public sealed class CompanyService : ICompanyService
    {
        public const long MaxLogoBytes = 2 * 1024 * 1024;
        private static readonly Dictionary<string, string> LogoTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".svg"] = "image/svg+xml"
        };
        private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhoneRx = new(@"^\+?[0-9 ()\-]{6,20}$", RegexOptions.Compiled);
        private static readonly Regex UnsafeSvg = new(@"<\s*script|\son[a-z]+\s*=|javascript:|<\s*foreignObject|<\s*iframe|<\s*embed|<\s*object",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly AxpenseDbContext _db;
        private readonly IFileStorage _storage;

        public CompanyService(AxpenseDbContext db, IFileStorage storage)
        {
            _db = db;
            _storage = storage;
        }

        private static int Active => (int)CurrentStatusType.Active;
        private static DateTime Today => DateTime.UtcNow.Date;

        // =====================================================================
        // Profile
        // =====================================================================
        public CompanyOptionsDto Options() => new(
            CompanyOptions.Industries, CompanyOptions.Sizes,
            CompanyOptions.TimeZones.Select(t => new ValueLabel(t.Id, t.Label)).ToList(),
            CompanyOptions.Currencies.Select(t => new ValueLabel(t.Code, t.Label)).ToList(),
            CompanyOptions.DateFormats,
            CompanyOptions.DistanceUnits.Select(t => new ValueLabel(t.Code, t.Label)).ToList(),
            Enumerable.Range(1, 12).Select(m => new ValueLabel(m.ToString(), CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m))).ToList());

        private async Task<OrganizationSettings> SettingsAsync(Guid organizationId, Guid userId, bool track, CancellationToken ct)
        {
            var q = _db.OrganizationSettings.Where(x => x.OrganizationId == organizationId);
            var s = track ? await q.FirstOrDefaultAsync(ct) : await q.AsNoTracking().FirstOrDefaultAsync(ct);
            if (s is not null) return s;
            var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizationId, ct);
            s = new OrganizationSettings
            {
                OrganizationId = organizationId, CompanyName = org?.Name ?? "", CurrentState = Active, CreatedBy = userId, CreatedAt = DateTime.UtcNow,
                // Workspaces created before onboarding existed are treated as already set up.
                OnboardingCompletedAt = DateTime.UtcNow
            };
            if (track) _db.OrganizationSettings.Add(s);
            return s;
        }

        private static CompanyProfileDto ToDto(OrganizationSettings s) => new(
            s.CompanyName, s.LegalName, s.Industry, s.CompanySize, s.CommercialRegistrationNo, s.TaxId, s.FoundedYear,
            s.LogoKey is not null, s.LogoKey is null ? null : (s.UpdatedAt ?? s.CreatedAt).Ticks.ToString(),
            s.Address, s.City, s.Country, s.Phone, s.Email, s.Website,
            s.TimeZone, s.Currency, s.FiscalYearStartMonth, s.DateFormat, s.DistanceUnit, s.UpdatedAt);

        public async Task<CompanyProfileDto> GetProfileAsync(Guid organizationId, CancellationToken ct = default) =>
            ToDto(await SettingsAsync(organizationId, Guid.Empty, false, ct));

        private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        public async Task<ServiceResult<CompanyProfileDto>> SaveProfileAsync(Guid organizationId, Guid userId, CompanyProfileRequest r, CancellationToken ct = default)
        {
            var e = new Dictionary<string, string>();
            var name = Clean(r.CompanyName);
            if (name is null) e["companyName"] = "Company name is required.";
            else if (name.Length > 120) e["companyName"] = "Company name must be 120 characters or fewer.";
            void Len(string field, string? v, int max, string label) { if (v is not null && v.Trim().Length > max) e[field] = $"{label} must be {max} characters or fewer."; }
            Len("legalName", r.LegalName, 160, "Legal name");
            Len("commercialRegistrationNo", r.CommercialRegistrationNo, 40, "Commercial registration no.");
            Len("taxId", r.TaxId, 40, "Tax ID");
            Len("address", r.Address, 250, "Address");
            Len("city", r.City, 80, "City");
            Len("country", r.Country, 80, "Country");
            if (Clean(r.Industry) is { } ind && !CompanyOptions.Industries.Contains(ind)) e["industry"] = "Choose an industry from the list.";
            if (Clean(r.CompanySize) is { } size && !CompanyOptions.Sizes.Contains(size)) e["companySize"] = "Choose a company size from the list.";
            if (r.FoundedYear is { } y && (y < 1800 || y > DateTime.UtcNow.Year)) e["foundedYear"] = $"Founded must be between 1800 and {DateTime.UtcNow.Year}.";
            if (Clean(r.Phone) is { } phone && !PhoneRx.IsMatch(phone)) e["phone"] = "Enter a valid phone number.";
            if (Clean(r.Email) is { } email && (email.Length > 200 || !EmailRx.IsMatch(email))) e["email"] = "Enter a valid email address.";
            var website = Clean(r.Website);
            if (website is not null)
            {
                if (!website.Contains("://")) website = "https://" + website;
                if (website.Length > 200 || !Uri.TryCreate(website, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") || !uri.Host.Contains('.'))
                    e["website"] = "Enter a valid website address.";
            }
            if (!CompanyOptions.TimeZones.Any(t => t.Id == r.TimeZone)) e["timeZone"] = "Choose a time zone from the list.";
            if (!CompanyOptions.Currencies.Any(t => t.Code == r.Currency)) e["currency"] = "Choose a currency from the list.";
            if (r.FiscalYearStartMonth is not (>= 1 and <= 12)) e["fiscalYearStartMonth"] = "Choose the month the fiscal year starts.";
            if (!CompanyOptions.DateFormats.Contains(r.DateFormat)) e["dateFormat"] = "Choose a date format from the list.";
            if (!CompanyOptions.DistanceUnits.Any(t => t.Code == r.DistanceUnit)) e["distanceUnit"] = "Choose a distance unit.";
            if (e.Count > 0) return ServiceResult<CompanyProfileDto>.Invalid(e);

            var s = await SettingsAsync(organizationId, userId, true, ct);
            s.CompanyName = name!;
            s.LegalName = Clean(r.LegalName);
            s.Industry = Clean(r.Industry);
            s.CompanySize = Clean(r.CompanySize);
            s.CommercialRegistrationNo = Clean(r.CommercialRegistrationNo);
            s.TaxId = Clean(r.TaxId);
            s.FoundedYear = r.FoundedYear;
            s.Address = Clean(r.Address);
            s.City = Clean(r.City);
            s.Country = Clean(r.Country);
            s.Phone = Clean(r.Phone);
            s.Email = Clean(r.Email)?.ToLowerInvariant();
            s.Website = website;
            s.TimeZone = r.TimeZone!;
            s.Currency = r.Currency!;
            s.FiscalYearStartMonth = r.FiscalYearStartMonth!.Value;
            s.DateFormat = r.DateFormat!;
            s.DistanceUnit = r.DistanceUnit!;
            s.UpdatedAt = DateTime.UtcNow;
            s.UpdatedBy = userId;

            // The organization's display name follows the company name (sidebar, invoices).
            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
            if (org is not null) org.Name = s.CompanyName;
            await AuditAsync(organizationId, userId, "Updated company profile", "OrganizationSettings", s.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<CompanyProfileDto>.Ok(ToDto(s));
        }

        /// <summary>
        /// Finishes the onboarding wizard. With a profile it is validated and saved like the Company page;
        /// without one (Skip) the wizard is simply marked as done.
        /// </summary>
        public async Task<ServiceResult<CompanyProfileDto>> CompleteOnboardingAsync(Guid organizationId, Guid userId, CompanyProfileRequest? request, CancellationToken ct = default)
        {
            if (request is not null)
            {
                var saved = await SaveProfileAsync(organizationId, userId, request, ct);
                if (!saved.Succeeded) return saved;
            }
            var s = await SettingsAsync(organizationId, userId, true, ct);
            s.OnboardingCompletedAt ??= DateTime.UtcNow;
            await AuditAsync(organizationId, userId, request is null ? "Skipped onboarding" : "Completed onboarding", "OrganizationSettings", s.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<CompanyProfileDto>.Ok(ToDto(s));
        }

        public async Task<ServiceResult<CompanyProfileDto>> SetLogoAsync(Guid organizationId, Guid userId, FileUpload file, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            string? error = null;
            if (file.Length <= 0) error = "The uploaded file is empty.";
            else if (file.Length > MaxLogoBytes) error = "The logo must be 2 MB or smaller.";
            else if (!LogoTypes.ContainsKey(ext)) error = "Only PNG, JPG or SVG files are allowed.";
            else error = await InspectLogoAsync(file.Content, ext);
            if (error is not null) return ServiceResult<CompanyProfileDto>.Invalid(new Dictionary<string, string> { ["logo"] = error });

            var s = await SettingsAsync(organizationId, userId, true, ct);
            var oldKey = s.LogoKey;
            file.Content.Position = 0;
            s.LogoKey = await _storage.SaveAsync(organizationId, "company", file.Content, ext.TrimStart('.'), ct);
            s.LogoContentType = LogoTypes[ext];
            s.LogoUrl = null;
            s.UpdatedAt = DateTime.UtcNow;
            s.UpdatedBy = userId;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                await _storage.DeleteAsync(s.LogoKey, ct);
                throw;
            }
            if (oldKey is not null) await _storage.DeleteAsync(oldKey, ct);
            return ServiceResult<CompanyProfileDto>.Ok(ToDto(s));
        }

        /// <summary>Checks the file really is the declared image type; SVGs must not carry scripts or event handlers.</summary>
        private static async Task<string?> InspectLogoAsync(Stream content, string ext)
        {
            content.Position = 0;
            if (ext == ".svg")
            {
                using var reader = new StreamReader(content, Encoding.UTF8, true, 4096, leaveOpen: true);
                var text = await reader.ReadToEndAsync();
                if (!text.Contains("<svg", StringComparison.OrdinalIgnoreCase)) return "The file is not a valid SVG image.";
                if (UnsafeSvg.IsMatch(text)) return "The SVG contains scripts or event handlers and can't be used.";
                return null;
            }
            var head = new byte[8];
            var read = await content.ReadAsync(head.AsMemory(0, 8));
            var ok = ext == ".png"
                ? read >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47
                : read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;
            return ok ? null : "The file content doesn't match its image type.";
        }

        public async Task<ServiceResult<CompanyProfileDto>> DeleteLogoAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
        {
            var s = await SettingsAsync(organizationId, userId, true, ct);
            var key = s.LogoKey;
            s.LogoKey = null;
            s.LogoContentType = null;
            s.LogoUrl = null;
            s.UpdatedAt = DateTime.UtcNow;
            s.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);
            if (key is not null) await _storage.DeleteAsync(key, ct);
            return ServiceResult<CompanyProfileDto>.Ok(ToDto(s));
        }

        public async Task<ServiceResult<FileDownload>> OpenLogoAsync(Guid organizationId, CancellationToken ct = default)
        {
            var s = await _db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
            if (s?.LogoKey is null) return ServiceResult<FileDownload>.NotFound("No logo uploaded.");
            var stream = await _storage.OpenReadAsync(s.LogoKey, ct);
            if (stream is null) return ServiceResult<FileDownload>.NotFound("Logo file is missing.");
            return ServiceResult<FileDownload>.Ok(new FileDownload(stream, "logo" + Path.GetExtension(s.LogoKey), s.LogoContentType ?? "application/octet-stream"));
        }

        // =====================================================================
        // Subscription
        // =====================================================================
        private static DateTime PeriodEnd(DateTime start, string cycle) => cycle == SubscriptionPlans.CycleAnnual ? start.AddYears(1) : start.AddMonths(1);

        /// <summary>
        /// Loads (or creates) the subscription and applies any renewals that fell due since it was last read.
        /// NOTE: the default for an organization with no subscription row is Business / monthly — a placeholder until sign-up picks a plan.
        /// </summary>
        private async Task<Subscription> LoadAsync(Guid organizationId, Guid userId, CancellationToken ct)
        {
            var sub = await _db.Subscriptions.FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
            var changed = false;
            if (sub is null)
            {
                var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizationId, ct);
                var since = (org?.CreatedAtUtc ?? DateTime.UtcNow).Date;
                var owner = await _db.Users.AsNoTracking().Where(u => u.OrganizationId == organizationId && !u.IsDeleted)
                    .OrderBy(u => u.CreatedAt).Select(u => u.Email).FirstOrDefaultAsync(ct);
                sub = new Subscription
                {
                    OrganizationId = organizationId, CustomerSince = since, CurrentPeriodStart = Today,
                    CurrentPeriodEnd = PeriodEnd(Today, SubscriptionPlans.CycleMonthly), BillingEmail = owner,
                    CurrentState = Active, CreatedBy = userId, CreatedAt = DateTime.UtcNow
                };
                _db.Subscriptions.Add(sub);
                await AddInvoiceAsync(sub, userId, $"{Plan(sub).Name} plan · Monthly", ct);
                changed = true;
            }

            var guard = 0;
            while (sub.Status != SubscriptionPlans.StatusCancelled && sub.CurrentPeriodEnd.Date <= Today && guard++ < 36)
            {
                if (sub.Status == SubscriptionPlans.StatusCancelling || !sub.AutoRenew)
                {
                    sub.Status = SubscriptionPlans.StatusCancelled;
                    sub.AutoRenew = false;
                }
                else
                {
                    sub.CurrentPeriodStart = sub.CurrentPeriodEnd.Date;
                    sub.CurrentPeriodEnd = PeriodEnd(sub.CurrentPeriodStart, sub.BillingCycle);
                    await AddInvoiceAsync(sub, userId, $"{Plan(sub).Name} plan · {CycleLabel(sub.BillingCycle)} renewal", ct);
                }
                changed = true;
            }
            if (changed)
            {
                sub.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            return sub;
        }

        private static SubscriptionPlans.Plan Plan(Subscription s) => SubscriptionPlans.Find(s.PlanKey) ?? SubscriptionPlans.All[1];
        private static string CycleLabel(string cycle) => cycle == SubscriptionPlans.CycleAnnual ? "Annual" : "Monthly";

        private async Task AddInvoiceAsync(Subscription sub, Guid userId, string description, CancellationToken ct)
        {
            var issued = sub.CurrentPeriodStart.Date;
            var year = issued.Year;
            var maxDb = await _db.SubscriptionInvoices.Where(i => i.OrganizationId == sub.OrganizationId && i.Year == year).MaxAsync(i => (int?)i.Number, ct) ?? 0;
            var maxLocal = _db.SubscriptionInvoices.Local.Where(i => i.OrganizationId == sub.OrganizationId && i.Year == year).Select(i => i.Number).DefaultIfEmpty(0).Max();
            _db.SubscriptionInvoices.Add(new SubscriptionInvoice
            {
                OrganizationId = sub.OrganizationId, Year = year, Number = Math.Max(maxDb, maxLocal) + 1, IssueDate = issued,
                Description = description, PlanKey = sub.PlanKey, BillingCycle = sub.BillingCycle,
                PeriodStart = sub.CurrentPeriodStart, PeriodEnd = sub.CurrentPeriodEnd,
                Amount = Plan(sub).Price(sub.BillingCycle), Currency = "EGP", Status = SubscriptionPlans.InvoiceIssued,
                CurrentState = Active, CreatedBy = userId, CreatedAt = DateTime.UtcNow
            });
        }

        private async Task<UsageDto> UsageAsync(Guid organizationId, SubscriptionPlans.Plan plan, CancellationToken ct)
        {
            var (vehicles, seats) = await PlanLimits.CountsAsync(_db, organizationId, ct);
            var bytes = await _storage.UsageBytesAsync(organizationId, ct);
            return new UsageDto(vehicles, plan.Vehicles, seats, plan.Seats, bytes, plan.StorageGb * 1024L * 1024 * 1024);
        }

        private async Task<SubscriptionDto> ToDtoAsync(Subscription s, CancellationToken ct)
        {
            var plan = Plan(s);
            var days = Math.Max(0, (int)(s.CurrentPeriodEnd.Date - Today).TotalDays);
            var plans = SubscriptionPlans.All.Select(p => new PlanDto(p.Key, p.Name, p.Monthly, p.Price(SubscriptionPlans.CycleAnnual),
                p.Vehicles, p.Seats, p.StorageGb, p.Popular, p.Features)).ToList();
            return new SubscriptionDto(s.PlanKey, plan.Name, s.BillingCycle, s.Status, s.AutoRenew, plan.Price(s.BillingCycle), "EGP",
                s.CustomerSince, s.CurrentPeriodStart, s.CurrentPeriodEnd, days, await UsageAsync(s.OrganizationId, plan, ct), plans);
        }

        public async Task<SubscriptionDto> GetSubscriptionAsync(Guid organizationId, Guid userId, CancellationToken ct = default) =>
            await ToDtoAsync(await LoadAsync(organizationId, userId, ct), ct);

        public async Task<ServiceResult<SubscriptionDto>> ChangePlanAsync(Guid organizationId, Guid userId, ChangePlanRequest r, CancellationToken ct = default)
        {
            var plan = SubscriptionPlans.Find(r.PlanKey ?? "");
            var cycle = r.BillingCycle ?? "";
            var e = new Dictionary<string, string>();
            if (plan is null) e["planKey"] = "Choose a plan.";
            if (!SubscriptionPlans.Cycles.Contains(cycle)) e["billingCycle"] = "Choose monthly or annual billing.";
            if (e.Count > 0) return ServiceResult<SubscriptionDto>.Invalid(e);

            var sub = await LoadAsync(organizationId, userId, ct);
            if (sub.Status == SubscriptionPlans.StatusActive && sub.PlanKey == plan!.Key && sub.BillingCycle == cycle)
                return ServiceResult<SubscriptionDto>.Conflict("PLAN_UNCHANGED", "This is already your current plan.");

            var (vehicles, seats) = await PlanLimits.CountsAsync(_db, organizationId, ct);
            if (plan!.Vehicles is { } vl && vehicles > vl)
                return ServiceResult<SubscriptionDto>.Conflict("PLAN_LIMIT_EXCEEDED",
                    $"You have {vehicles} vehicles; the {plan.Name} plan allows {vl}. Retire or delete vehicles before switching.");
            if (plan.Seats is { } sl && seats > sl)
                return ServiceResult<SubscriptionDto>.Conflict("PLAN_LIMIT_EXCEEDED",
                    $"You have {seats} active users; the {plan.Name} plan allows {sl}. Deactivate users before switching.");
            var storage = await _storage.UsageBytesAsync(organizationId, ct);
            if (storage > plan.StorageGb * 1024L * 1024 * 1024)
                return ServiceResult<SubscriptionDto>.Conflict("PLAN_LIMIT_EXCEEDED", $"Your files exceed the {plan.Name} plan's {plan.StorageGb} GB storage.");

            var from = SubscriptionPlans.Find(sub.PlanKey);
            var verb = sub.Status == SubscriptionPlans.StatusCancelled ? "Reactivated"
                : SubscriptionPlans.Rank(plan.Key) > SubscriptionPlans.Rank(sub.PlanKey) ? "Upgrade"
                : SubscriptionPlans.Rank(plan.Key) < SubscriptionPlans.Rank(sub.PlanKey) ? "Downgrade" : "Billing change";
            sub.PlanKey = plan.Key;
            sub.BillingCycle = cycle;
            sub.Status = SubscriptionPlans.StatusActive;
            sub.AutoRenew = true;
            sub.CurrentPeriodStart = Today;
            sub.CurrentPeriodEnd = PeriodEnd(Today, cycle);
            sub.UpdatedAt = DateTime.UtcNow;
            sub.UpdatedBy = userId;
            await AddInvoiceAsync(sub, userId, $"{plan.Name} plan · {CycleLabel(cycle)} ({verb})", ct);
            await AuditAsync(organizationId, userId, $"Changed plan from {from?.Name} to {plan.Name} ({CycleLabel(cycle)})", "Subscription", sub.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<SubscriptionDto>.Ok(await ToDtoAsync(sub, ct));
        }

        public async Task<ServiceResult<SubscriptionDto>> SetAutoRenewAsync(Guid organizationId, Guid userId, bool autoRenew, CancellationToken ct = default)
        {
            var sub = await LoadAsync(organizationId, userId, ct);
            if (sub.Status != SubscriptionPlans.StatusActive)
                return ServiceResult<SubscriptionDto>.Conflict("SUBSCRIPTION_NOT_ACTIVE", "Resume the subscription before changing auto-renew.");
            sub.AutoRenew = autoRenew;
            sub.UpdatedAt = DateTime.UtcNow;
            sub.UpdatedBy = userId;
            await AuditAsync(organizationId, userId, autoRenew ? "Turned on auto-renew" : "Turned off auto-renew", "Subscription", sub.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<SubscriptionDto>.Ok(await ToDtoAsync(sub, ct));
        }

        public async Task<ServiceResult<SubscriptionDto>> CancelAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
        {
            var sub = await LoadAsync(organizationId, userId, ct);
            if (sub.Status != SubscriptionPlans.StatusActive)
                return ServiceResult<SubscriptionDto>.Conflict("SUBSCRIPTION_NOT_ACTIVE", "The subscription is already cancelled.");
            sub.Status = SubscriptionPlans.StatusCancelling;
            sub.AutoRenew = false;
            sub.UpdatedAt = DateTime.UtcNow;
            sub.UpdatedBy = userId;
            await AuditAsync(organizationId, userId, $"Cancelled subscription (ends {sub.CurrentPeriodEnd:yyyy-MM-dd})", "Subscription", sub.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<SubscriptionDto>.Ok(await ToDtoAsync(sub, ct));
        }

        public async Task<ServiceResult<SubscriptionDto>> ResumeAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
        {
            var sub = await LoadAsync(organizationId, userId, ct);
            if (sub.Status != SubscriptionPlans.StatusCancelling)
                return ServiceResult<SubscriptionDto>.Conflict("SUBSCRIPTION_NOT_CANCELLING",
                    sub.Status == SubscriptionPlans.StatusCancelled ? "The subscription has ended — choose a plan to reactivate it." : "The subscription is active.");
            sub.Status = SubscriptionPlans.StatusActive;
            sub.AutoRenew = true;
            sub.UpdatedAt = DateTime.UtcNow;
            sub.UpdatedBy = userId;
            await AuditAsync(organizationId, userId, "Resumed subscription", "Subscription", sub.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<SubscriptionDto>.Ok(await ToDtoAsync(sub, ct));
        }

        // =====================================================================
        // Billing
        // =====================================================================
        public static string InvoiceCode(SubscriptionInvoice i) => $"INV-{i.Year}-{i.Number:0000}";

        public async Task<BillingDto> GetBillingAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
        {
            var sub = await LoadAsync(organizationId, userId, ct);
            return await BillingDtoAsync(sub, ct);
        }

        private async Task<BillingDto> BillingDtoAsync(Subscription sub, CancellationToken ct)
        {
            var invoices = await _db.SubscriptionInvoices.AsNoTracking()
                .Where(i => i.OrganizationId == sub.OrganizationId && i.CurrentState == Active)
                .OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Year).ThenByDescending(i => i.Number)
                .Take(100).ToListAsync(ct);
            return new BillingDto(sub.CardBrand, sub.CardLast4, sub.CardExpiry, sub.CardHolder, sub.BillingEmail,
                invoices.Select(i => new InvoiceDto(i.Id, InvoiceCode(i), i.IssueDate, i.Description, i.Amount, i.Currency, i.Status, i.PeriodStart, i.PeriodEnd)).ToList());
        }

        private static readonly string[] Brands = ["Visa", "Mastercard", "Amex", "Meeza", "Card"];

        public async Task<ServiceResult<BillingDto>> SetPaymentMethodAsync(Guid organizationId, Guid userId, PaymentMethodRequest r, CancellationToken ct = default)
        {
            var e = new Dictionary<string, string>();
            if (!Brands.Contains(r.Brand ?? "")) e["brand"] = "Unsupported card brand.";
            if (r.Last4 is null || !Regex.IsMatch(r.Last4, @"^\d{4}$")) e["cardNumber"] = "Enter a valid card number.";
            var holder = Clean(r.Holder);
            if (holder is null) e["holder"] = "Cardholder name is required.";
            else if (holder.Length > 120) e["holder"] = "Cardholder name must be 120 characters or fewer.";
            var m = Regex.Match(r.Expiry ?? "", @"^(0[1-9]|1[0-2])/(\d{2})$");
            if (!m.Success) e["expiry"] = "Enter the expiry as MM/YY.";
            else
            {
                var month = int.Parse(m.Groups[1].Value);
                var year = 2000 + int.Parse(m.Groups[2].Value);
                if (new DateTime(year, month, 1).AddMonths(1) <= Today) e["expiry"] = "This card has expired.";
            }
            if (e.Count > 0) return ServiceResult<BillingDto>.Invalid(e);

            var sub = await LoadAsync(organizationId, userId, ct);
            sub.CardBrand = r.Brand;
            sub.CardLast4 = r.Last4;
            sub.CardExpiry = r.Expiry;
            sub.CardHolder = holder!.ToUpperInvariant();
            sub.UpdatedAt = DateTime.UtcNow;
            sub.UpdatedBy = userId;
            await AuditAsync(organizationId, userId, $"Updated payment method ({r.Brand} •••• {r.Last4})", "Subscription", sub.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BillingDto>.Ok(await BillingDtoAsync(sub, ct));
        }

        public async Task<ServiceResult<BillingDto>> SetBillingEmailAsync(Guid organizationId, Guid userId, string? email, CancellationToken ct = default)
        {
            var v = Clean(email)?.ToLowerInvariant();
            if (v is null || v.Length > 200 || !EmailRx.IsMatch(v))
                return ServiceResult<BillingDto>.Invalid(new Dictionary<string, string> { ["email"] = "Enter a valid email address." });
            var sub = await LoadAsync(organizationId, userId, ct);
            sub.BillingEmail = v;
            sub.UpdatedAt = DateTime.UtcNow;
            sub.UpdatedBy = userId;
            await AuditAsync(organizationId, userId, "Updated billing contact", "Subscription", sub.Id.ToString());
            await _db.SaveChangesAsync(ct);
            return ServiceResult<BillingDto>.Ok(await BillingDtoAsync(sub, ct));
        }

        public async Task<ServiceResult<InvoiceDocument>> InvoiceDocumentAsync(Guid organizationId, Guid invoiceId, CancellationToken ct = default)
        {
            var inv = await _db.SubscriptionInvoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.OrganizationId == organizationId && i.CurrentState == Active, ct);
            if (inv is null) return ServiceResult<InvoiceDocument>.NotFound("Invoice not found.");
            var s = await SettingsAsync(organizationId, Guid.Empty, false, ct);
            var sub = await _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
            string H(string? v) => WebUtility.HtmlEncode(v ?? "");
            string Money(decimal v) => v.ToString("#,##0.00", CultureInfo.InvariantCulture);
            var code = InvoiceCode(inv);
            var billTo = string.Join("<br>", new[] { s.LegalName ?? s.CompanyName, s.Address, string.Join(", ", new[] { s.City, s.Country }.Where(x => !string.IsNullOrWhiteSpace(x))),
                s.TaxId is null ? null : "Tax ID: " + s.TaxId, sub?.BillingEmail }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(H));
            var html = $$"""
<!doctype html><html><head><meta charset="utf-8"><title>{{code}}</title>
<style>body{font:14px/1.5 system-ui,Segoe UI,Arial,sans-serif;color:#1f2a37;margin:40px auto;max-width:760px}
h1{font-size:22px;margin:0}.muted{color:#6b7785}.row{display:flex;justify-content:space-between;gap:24px;margin:28px 0}
table{width:100%;border-collapse:collapse;margin-top:12px}th,td{padding:10px;border-bottom:1px solid #e5e9ef;text-align:left}
th{font-size:12px;text-transform:uppercase;color:#6b7785}.r{text-align:right}.total{font-size:18px;font-weight:700}
.badge{display:inline-block;padding:2px 10px;border-radius:99px;background:#e8f7ee;color:#11875a;font-size:12px;font-weight:600}
@media print{.noprint{display:none} }</style></head><body>
<div class="row"><div><h1>Axpense</h1><div class="muted">Fleet management platform</div></div>
<div class="r"><h1>Invoice</h1><div>{{code}}</div><div class="muted">Issued {{inv.IssueDate:dd MMM yyyy}}</div><span class="badge">{{H(inv.Status)}}</span></div></div>
<div><div class="muted">Bill to</div>{{billTo}}</div>
<table><thead><tr><th>Description</th><th>Period</th><th class="r">Amount ({{H(inv.Currency)}})</th></tr></thead>
<tbody><tr><td>{{H(inv.Description)}}</td><td>{{inv.PeriodStart:dd MMM yyyy}} – {{inv.PeriodEnd:dd MMM yyyy}}</td><td class="r">{{Money(inv.Amount)}}</td></tr></tbody>
<tfoot><tr><td colspan="2" class="r total">Total</td><td class="r total">{{H(inv.Currency)}} {{Money(inv.Amount)}}</td></tr></tfoot></table>
<p class="muted">Amounts are shown as billed by the plan price list. This document is generated by Axpense and is not a tax invoice unless issued by the payment provider.</p>
<p class="noprint"><button onclick="window.print()">Print / Save as PDF</button></p>
</body></html>
""";
            return ServiceResult<InvoiceDocument>.Ok(new InvoiceDocument($"{code}.html", html));
        }

        // =====================================================================
        private Task AuditAsync(Guid organizationId, Guid userId, string action, string entity, string? id)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                OrganizationId = organizationId, UserId = userId, Action = action, EntityType = entity, EntityId = id,
                CurrentState = Active, CreatedBy = userId, CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        }
    }

    /// <summary>Plan limit checks shared by vehicle and user creation.</summary>
    public static class PlanLimits
    {
        /// <summary>Vehicles that count toward the plan (active, not retired) and active users (seats).</summary>
        public static async Task<(int Vehicles, int Seats)> CountsAsync(AxpenseDbContext db, Guid organizationId, CancellationToken ct)
        {
            var active = (int)CurrentStatusType.Active;
            var vehicles = await db.Vehicles.CountAsync(v => v.OrganizationId == organizationId && v.CurrentState == active && v.Status != VehicleConstants.StatusRetired, ct);
            var seats = await db.Users.CountAsync(u => u.OrganizationId == organizationId && !u.IsDeleted, ct);
            return (vehicles, seats);
        }

        /// <summary>Returns an error message when adding one more vehicle/seat would exceed the plan; null when allowed.</summary>
        public static async Task<string?> CheckAsync(AxpenseDbContext db, Guid organizationId, bool vehicle, CancellationToken ct)
        {
            var sub = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
            var plan = SubscriptionPlans.Find(sub?.PlanKey ?? SubscriptionPlans.Business) ?? SubscriptionPlans.All[1];
            var (vehicles, seats) = await CountsAsync(db, organizationId, ct);
            if (vehicle && plan.Vehicles is { } vl && vehicles >= vl)
                return $"Your {plan.Name} plan includes {vl} vehicles. Upgrade the plan in Administration → Company to add more.";
            if (!vehicle && plan.Seats is { } sl && seats >= sl)
                return $"Your {plan.Name} plan includes {sl} team members. Upgrade the plan in Administration → Company to add more.";
            return null;
        }
    }
}
