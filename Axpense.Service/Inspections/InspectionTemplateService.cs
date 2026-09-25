using Axpense.Data.Constants;
using Axpense.Data.Entities.InspectionEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Service.Common;
using Axpense.Service.Inspections.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Inspections
{
    public interface IInspectionTemplateService
    {
        InspectionOptionsDto Options();
        Task<List<TemplateDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<TemplateDto>> GetAsync(Guid organizationId, Guid templateId, CancellationToken ct = default);
        Task<ServiceResult<TemplateDto>> CreateAsync(Guid organizationId, Guid userId, TemplateUpsertRequest request, CancellationToken ct = default);
        Task<ServiceResult<TemplateDto>> UpdateAsync(Guid organizationId, Guid userId, Guid templateId, TemplateUpsertRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid templateId, CancellationToken ct = default);
        /// <summary>Returns the built-in hand-off template, creating it on first use.</summary>
        Task<Guid> EnsureHandoffTemplateAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    }

    /// <summary>
    /// Inspection templates (checklists). Rules: a name (unique per organization), a scope ("All" or a
    /// vehicle category), 1–60 checks; every check is linked to a part of the catalogue; a gauge check
    /// needs a unit and a valid min ≤ max range. Runs copy the checklist, so edits and deletes never change history.
    /// </summary>
    public sealed class InspectionTemplateService : IInspectionTemplateService
    {
        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;

        public InspectionTemplateService(AxpenseDbContext db, IUnitOfWork uow)
        {
            _db = db;
            _uow = uow;
        }

        private static int Active => (int)CurrentStatusType.Active;
        private static readonly string[] Scopes = [InspectionConstants.ScopeAll, .. VehicleConstants.Categories];

        public InspectionOptionsDto Options() => new(Scopes,
            InspectionConstants.FieldTypes.Select(t => new FieldTypeOption(t, InspectionConstants.FieldLabel(t))).ToList());

        public async Task<List<TemplateDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
        {
            await EnsureHandoffTemplateAsync(organizationId, Guid.Empty, ct);
            var templates = await Query(organizationId).OrderByDescending(t => t.Active).ThenBy(t => t.Name).ToListAsync(ct);
            var runs = await RunCountsAsync(organizationId, ct);
            return templates.Select(t => ToDto(t, runs.GetValueOrDefault(t.Id))).ToList();
        }

        public async Task<ServiceResult<TemplateDto>> GetAsync(Guid organizationId, Guid templateId, CancellationToken ct = default)
        {
            var t = await Query(organizationId).FirstOrDefaultAsync(x => x.Id == templateId, ct);
            if (t is null) return ServiceResult<TemplateDto>.NotFound("Template not found.");
            var runs = await RunCountsAsync(organizationId, ct);
            return ServiceResult<TemplateDto>.Ok(ToDto(t, runs.GetValueOrDefault(t.Id)));
        }

        public async Task<ServiceResult<TemplateDto>> CreateAsync(Guid organizationId, Guid userId, TemplateUpsertRequest request, CancellationToken ct = default)
        {
            var errors = await ValidateAsync(organizationId, null, request, ct);
            if (errors.Count > 0) return ServiceResult<TemplateDto>.Invalid(errors);

            var next = (await _db.InspectionTemplates.Where(x => x.OrganizationId == organizationId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
            var t = new InspectionTemplate
            {
                OrganizationId = organizationId, Number = next, Name = request.Name.Trim(),
                Scope = string.IsNullOrWhiteSpace(request.Scope) ? InspectionConstants.ScopeAll : request.Scope!, Active = request.Active
            };
            await _uow.Repository<InspectionTemplate>().AddAsyncGetID(t, userId, ct);
            await AddItemsAsync(organizationId, userId, t.Id, request, ct);
            return await GetAsync(organizationId, t.Id, ct);
        }

        public async Task<ServiceResult<TemplateDto>> UpdateAsync(Guid organizationId, Guid userId, Guid templateId, TemplateUpsertRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<InspectionTemplate>();
            var t = await repo.GetByIdAsync(templateId, ct);
            if (t is null || t.OrganizationId != organizationId || t.CurrentState != Active) return ServiceResult<TemplateDto>.NotFound("Template not found.");
            var errors = await ValidateAsync(organizationId, templateId, request, ct);
            if (errors.Count > 0) return ServiceResult<TemplateDto>.Invalid(errors);

            t.Name = request.Name.Trim();
            // A built-in template stays active and applies to every vehicle.
            t.Scope = t.SystemKey is not null || string.IsNullOrWhiteSpace(request.Scope) ? InspectionConstants.ScopeAll : request.Scope!;
            t.Active = t.SystemKey is not null || request.Active;
            await repo.UpdateAsync(t, userId, ct);

            // Checks are replaced as a whole; runs already started keep their own copy.
            var old = await _db.InspectionTemplateItems.Where(x => x.OrganizationId == organizationId && x.TemplateId == templateId).ToListAsync(ct);
            _db.InspectionTemplateItems.RemoveRange(old);
            await _db.SaveChangesAsync(ct);
            await AddItemsAsync(organizationId, userId, templateId, request, ct);
            return await GetAsync(organizationId, templateId, ct);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid templateId, CancellationToken ct = default)
        {
            var t = await Query(organizationId).FirstOrDefaultAsync(x => x.Id == templateId, ct);
            if (t is null) return ServiceResult<bool>.NotFound("Template not found.");
            if (t.SystemKey is not null)
                return ServiceResult<bool>.Conflict("TEMPLATE_BUILT_IN", "This built-in template is used by the vehicle hand-off. Edit its checks instead of deleting it.");
            // Past and in-progress runs keep their checklist copy; their template link is cleared (FK set null).
            await _uow.Repository<InspectionTemplate>().DeleteAsync(templateId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<Guid> EnsureHandoffTemplateAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
        {
            var existing = await _db.InspectionTemplates.AsNoTracking()
                .Where(t => t.OrganizationId == organizationId && t.CurrentState == Active && t.SystemKey == InspectionConstants.SystemHandoff)
                .Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (existing is not null) return existing.Value;

            var parts = await _db.PresetParts.AsNoTracking().Where(p => p.OrganizationId == organizationId && p.CurrentState == Active)
                .ToListAsync(ct);
            var byName = parts.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var checks = InspectionConstants.HandoffChecks.Where(c => byName.ContainsKey(c.PartName)).ToList();
            if (checks.Count == 0 && parts.Count > 0) checks = [new("General condition walk-around", parts[0].Name, InspectionConstants.FieldPassFail, false)];
            if (checks.Count == 0) return Guid.Empty; // empty catalogue: nothing to link the checks to yet

            var name = InspectionConstants.HandoffTemplateName;
            if (await _db.InspectionTemplates.AnyAsync(t => t.OrganizationId == organizationId && t.CurrentState == Active && t.Name == name, ct))
                name += " (built-in)";
            var next = (await _db.InspectionTemplates.Where(x => x.OrganizationId == organizationId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
            var t = new InspectionTemplate
            {
                OrganizationId = organizationId, Number = next, Name = name, Scope = InspectionConstants.ScopeAll, Active = true,
                SystemKey = InspectionConstants.SystemHandoff, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
            };
            _db.InspectionTemplates.Add(t);
            _db.InspectionTemplateItems.AddRange(checks.Select((c, ix) => new InspectionTemplateItem
            {
                OrganizationId = organizationId, TemplateId = t.Id, Sort = ix + 1, Label = c.Label, PresetPartId = byName[c.PartName],
                FieldType = c.FieldType, Critical = c.Critical, IssueOnFail = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
            }));
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Created concurrently by another request (unique template number) — use that one.
                _db.ChangeTracker.Clear();
                return await _db.InspectionTemplates.AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.SystemKey == InspectionConstants.SystemHandoff)
                    .Select(x => x.Id).FirstAsync(ct);
            }
            return t.Id;
        }

        // ---------------------------------------------------------------- helpers

        private IQueryable<InspectionTemplate> Query(Guid organizationId) =>
            _db.InspectionTemplates.AsNoTracking()
                .Include(t => t.Items).ThenInclude(i => i.PresetPart)
                .Where(t => t.OrganizationId == organizationId && t.CurrentState == Active);

        private async Task<Dictionary<Guid, int>> RunCountsAsync(Guid organizationId, CancellationToken ct) =>
            await _db.Inspections.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.TemplateId != null && x.CurrentState == Active)
                .GroupBy(x => x.TemplateId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        private async Task AddItemsAsync(Guid organizationId, Guid userId, Guid templateId, TemplateUpsertRequest r, CancellationToken ct)
        {
            var names = await _db.PresetParts.AsNoTracking().Where(p => p.OrganizationId == organizationId).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
            var items = r.Items.Select((i, ix) => new InspectionTemplateItem
            {
                OrganizationId = organizationId, TemplateId = templateId, Sort = ix + 1,
                Label = string.IsNullOrWhiteSpace(i.Label) ? names[i.PresetPartId!.Value] : i.Label.Trim(),
                PresetPartId = i.PresetPartId!.Value,
                FieldType = i.FieldType ?? InspectionConstants.FieldPassFail,
                Critical = i.Critical,
                Unit = i.FieldType == InspectionConstants.FieldGauge ? i.Unit?.Trim() : null,
                Min = i.FieldType == InspectionConstants.FieldGauge ? i.Min : null,
                Max = i.FieldType == InspectionConstants.FieldGauge ? i.Max : null,
                IssueOnFail = i.IssueOnFail,
                CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
            }).ToList();
            // Children added through their DbSet (BaseEntity pre-sets Id, so EF would otherwise treat them as updates).
            _db.InspectionTemplateItems.AddRange(items);
            await _db.SaveChangesAsync(ct);
        }

        private async Task<Dictionary<string, string>> ValidateAsync(Guid organizationId, Guid? templateId, TemplateUpsertRequest r, CancellationToken ct)
        {
            var e = new Dictionary<string, string>();
            var name = r.Name?.Trim() ?? "";
            if (name.Length == 0) e["name"] = "Template name is required.";
            else if (name.Length > 150) e["name"] = "Template name must be 150 characters or fewer.";
            else if (await _db.InspectionTemplates.AnyAsync(t => t.OrganizationId == organizationId && t.CurrentState == Active && t.Name == name && t.Id != templateId, ct))
                e["name"] = "A template with this name already exists.";
            if (!string.IsNullOrWhiteSpace(r.Scope) && !Scopes.Contains(r.Scope)) e["scope"] = $"Applies to must be one of: {string.Join(", ", Scopes)}.";

            if (r.Items is null || r.Items.Count == 0) { e["items"] = "A template needs at least one check."; return e; }
            if (r.Items.Count > InspectionConstants.MaxChecks) e["items"] = $"A template can have at most {InspectionConstants.MaxChecks} checks.";

            var partIds = r.Items.Where(i => i.PresetPartId != null).Select(i => i.PresetPartId!.Value).Distinct().ToList();
            var known = (await _db.PresetParts.AsNoTracking()
                .Where(p => p.OrganizationId == organizationId && p.CurrentState == Active && partIds.Contains(p.Id))
                .Select(p => p.Id).ToListAsync(ct)).ToHashSet();

            for (var ix = 0; ix < r.Items.Count; ix++)
            {
                var i = r.Items[ix];
                var k = $"items[{ix}]";
                if (i.PresetPartId is null) e[$"{k}.presetPartId"] = $"Check {ix + 1}: link a part from the catalogue.";
                else if (!known.Contains(i.PresetPartId.Value)) e[$"{k}.presetPartId"] = $"Check {ix + 1}: part not found in the catalogue.";
                if (i.Label is { Length: > 150 }) e[$"{k}.label"] = $"Check {ix + 1}: label must be 150 characters or fewer.";
                var type = i.FieldType ?? InspectionConstants.FieldPassFail;
                if (!InspectionConstants.FieldTypes.Contains(type)) e[$"{k}.fieldType"] = $"Check {ix + 1}: unknown field type.";
                if (type == InspectionConstants.FieldGauge)
                {
                    if (string.IsNullOrWhiteSpace(i.Unit)) e[$"{k}.unit"] = $"Check {ix + 1}: a gauge needs a unit (PSI, mm, bar…).";
                    else if (i.Unit.Trim().Length > 20) e[$"{k}.unit"] = $"Check {ix + 1}: unit must be 20 characters or fewer.";
                    if (i.Min is null || i.Max is null) e[$"{k}.min"] = $"Check {ix + 1}: set the expected range (min and max).";
                    else if (i.Min > i.Max) e[$"{k}.min"] = $"Check {ix + 1}: min can't be above max.";
                }
            }
            return e;
        }

        private static TemplateDto ToDto(InspectionTemplate t, int runs)
        {
            var items = t.Items.OrderBy(i => i.Sort).Select(i => new TemplateItemDto(
                i.Id, i.Sort, i.Label, i.PresetPartId, i.PresetPart.Name, i.PresetPart.NameAr, i.PresetPart.CategoryId,
                i.FieldType, InspectionConstants.FieldLabel(i.FieldType), i.Critical, i.Unit, i.Min, i.Max, i.IssueOnFail)).ToList();
            return new TemplateDto(t.Id, $"TPL-{t.Number:000}", t.Name, t.Scope, t.Active, t.SystemKey is not null, items.Count, items.Count(i => i.Critical), runs, items);
        }
    }
}
