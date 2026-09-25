using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Data.Entities.InspectionEntities;
using Axpense.Data.Entities.MaintenanceRecord;
using Axpense.Data.Entities.VehicleEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Service.Common;
using Axpense.Service.Drivers.Dtos;
using Axpense.Service.Inspections.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Inspections
{
    public interface IInspectionService
    {
        Task<InspectionListResponse> ListAsync(Guid organizationId, InspectionListQuery query, CancellationToken ct = default);
        Task<ServiceResult<InspectionDetailDto>> GetAsync(Guid organizationId, Guid inspectionId, CancellationToken ct = default);
        Task<ServiceResult<InspectionDetailDto>> StartAsync(Guid organizationId, Guid userId, InspectionStartRequest request, CancellationToken ct = default);
        Task<ServiceResult<InspectionItemDto>> AnswerAsync(Guid organizationId, Guid userId, Guid inspectionId, Guid itemId, InspectionAnswerRequest request, CancellationToken ct = default);
        Task<ServiceResult<InspectionItemDto>> SetPhotoAsync(Guid organizationId, Guid userId, Guid inspectionId, Guid itemId, FileUpload file, CancellationToken ct = default);
        Task<ServiceResult<InspectionItemDto>> RemovePhotoAsync(Guid organizationId, Guid userId, Guid inspectionId, Guid itemId, CancellationToken ct = default);
        Task<ServiceResult<FileDownload>> OpenPhotoAsync(Guid organizationId, Guid inspectionId, Guid itemId, CancellationToken ct = default);
        Task<ServiceResult<InspectionCompleteResult>> CompleteAsync(Guid organizationId, Guid userId, Guid inspectionId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid inspectionId, CancellationToken ct = default);
    }

    /// <summary>
    /// Running inspections. Rules:
    /// <list type="bullet">
    /// <item>Only active templates whose scope is "All" or the vehicle's category can be run; retired vehicles can't be inspected.</item>
    /// <item>One run in progress per vehicle. The run is a server-side draft: answers and photos are saved as they are given.</item>
    /// <item>The odometer at inspection can't be below the vehicle's current reading.</item>
    /// <item>A gauge reading outside the expected range is a failure. A failed check needs photo evidence; a photo-only check needs its photo.</item>
    /// <item>On completion: a reading is logged, every failed check with "create an issue" raises an issue (High if critical, else Medium),
    /// and a failed critical check grounds the vehicle (Inoperable / Down) and schedules a corrective maintenance job.</item>
    /// <item>Deleting an inspection keeps the issues it raised.</item>
    /// </list>
    /// </summary>
    public sealed class InspectionService : IInspectionService
    {
        private static readonly Dictionary<string, string> PhotoTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
        };

        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;
        private readonly IFileStorage _storage;

        public InspectionService(AxpenseDbContext db, IUnitOfWork uow, IFileStorage storage)
        {
            _db = db;
            _uow = uow;
            _storage = storage;
        }

        private static DateTime Today => DateTime.UtcNow.Date;
        private static int Active => (int)CurrentStatusType.Active;

        // =====================================================================
        // Log
        // =====================================================================
        public async Task<InspectionListResponse> ListAsync(Guid organizationId, InspectionListQuery query, CancellationToken ct = default)
        {
            var rows = await _db.Inspections.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active
                            && (query.VehicleId == null || x.VehicleId == query.VehicleId)
                            && (query.TemplateId == null || x.TemplateId == query.TemplateId)
                            && (query.Status == null || query.Status == "" || x.Status == query.Status))
                .OrderByDescending(x => x.Status == InspectionConstants.StatusInProgress).ThenByDescending(x => x.InspectionDate).ThenByDescending(x => x.Number)
                .Select(x => new
                {
                    x.Id, x.Number, x.VehicleId, x.Vehicle.Make, x.Vehicle.Model, x.Vehicle.PlateNumber, x.TemplateId, x.TemplateName,
                    x.InspectionDate, x.Odometer, x.InspectorName, x.Status,
                    Results = x.Items.Select(i => i.Result).ToList()
                })
                .ToListAsync(ct);

            var items = rows.Select(r => new InspectionListItemDto(r.Id, Code(r.Number), r.VehicleId, $"{r.Make} {r.Model}".Trim(), r.PlateNumber,
                r.TemplateId, r.TemplateName, r.InspectionDate, r.Odometer, r.InspectorName, r.Status, Summarize(r.Results))).ToList();

            // Stats cover the whole organization, not the filtered view.
            var completed = await _db.Inspections.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.Status == InspectionConstants.StatusCompleted).CountAsync(ct);
            var inProgress = await _db.Inspections.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.Status == InspectionConstants.StatusInProgress).CountAsync(ct);
            var failedItems = await _db.InspectionItems.AsNoTracking()
                .Where(i => i.OrganizationId == organizationId && i.Result == InspectionConstants.ResultFail
                            && i.Inspection.CurrentState == Active && i.Inspection.Status == InspectionConstants.StatusCompleted)
                .CountAsync(ct);
            var templates = await _db.InspectionTemplates.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.CurrentState == Active)
                .Select(t => t.Active).ToListAsync(ct);
            var fleet = await _db.Vehicles.AsNoTracking()
                .Where(v => v.OrganizationId == organizationId && v.CurrentState == Active && v.Status != VehicleConstants.StatusRetired)
                .Select(v => v.Id).ToListAsync(ct);
            var inspected = (await _db.Inspections.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.CurrentState == Active && x.Status == InspectionConstants.StatusCompleted)
                .Select(x => x.VehicleId).Distinct().ToListAsync(ct)).ToHashSet();

            return new InspectionListResponse(items, new InspectionStatsDto(completed, failedItems, templates.Count, templates.Count(a => a),
                fleet.Count(id => !inspected.Contains(id)), fleet.Count, inProgress));
        }

        public async Task<ServiceResult<InspectionDetailDto>> GetAsync(Guid organizationId, Guid inspectionId, CancellationToken ct = default)
        {
            var x = await _db.Inspections.AsNoTracking().Include(i => i.Items).Include(i => i.Vehicle)
                .FirstOrDefaultAsync(i => i.Id == inspectionId && i.OrganizationId == organizationId && i.CurrentState == Active, ct);
            return x is null ? ServiceResult<InspectionDetailDto>.NotFound("Inspection not found.") : ServiceResult<InspectionDetailDto>.Ok(ToDetail(x));
        }

        // =====================================================================
        // Run
        // =====================================================================
        public async Task<ServiceResult<InspectionDetailDto>> StartAsync(Guid organizationId, Guid userId, InspectionStartRequest r, CancellationToken ct = default)
        {
            var e = new Dictionary<string, string>();
            Vehicle? v = null;
            InspectionTemplate? t = null;
            if (r.VehicleId is null) e["vehicleId"] = "Select a vehicle.";
            else
            {
                v = await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.VehicleId && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
                if (v is null) e["vehicleId"] = "Vehicle not found.";
                else if (v.Status == VehicleConstants.StatusRetired) e["vehicleId"] = "A retired vehicle can't be inspected.";
            }
            if (r.TemplateId is null) e["templateId"] = "Select a template.";
            else
            {
                t = await _db.InspectionTemplates.AsNoTracking().Include(x => x.Items).ThenInclude(i => i.PresetPart)
                    .FirstOrDefaultAsync(x => x.Id == r.TemplateId && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
                if (t is null) e["templateId"] = "Template not found.";
                else if (!t.Active) e["templateId"] = "This template is a draft — activate it before running it.";
                else if (t.Items.Count == 0) e["templateId"] = "This template has no checks.";
                else if (v is not null && t.Scope != InspectionConstants.ScopeAll && t.Scope != v.Category)
                    e["templateId"] = $"This template applies to {t.Scope} vehicles; {v.PlateNumber} is a {v.Category}.";
            }
            if (r.Odometer is null) e["odometer"] = "Odometer at inspection is required.";
            else if (r.Odometer < 0) e["odometer"] = "Odometer can't be negative.";
            else if (v?.CurrentOdometer is not null && r.Odometer < v.CurrentOdometer)
                e["odometer"] = $"Odometer can't be below the vehicle's current reading ({v.CurrentOdometer:N0} {v.ReadingUnit}).";
            var inspector = r.InspectorName?.Trim() ?? "";
            if (inspector.Length == 0) e["inspectorName"] = "Inspector is required.";
            else if (inspector.Length > 120) e["inspectorName"] = "Inspector must be 120 characters or fewer.";
            if (e.Count > 0) return ServiceResult<InspectionDetailDto>.Invalid(e);

            var draft = await _db.Inspections.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.VehicleId == v!.Id && x.CurrentState == Active && x.Status == InspectionConstants.StatusInProgress)
                .Select(x => (int?)x.Number).FirstOrDefaultAsync(ct);
            if (draft is not null)
                return ServiceResult<InspectionDetailDto>.Conflict("INSPECTION_IN_PROGRESS",
                    $"{Code(draft.Value)} is already in progress on {v!.PlateNumber}. Resume or discard it first.", "vehicleId");

            var number = (await _db.Inspections.Where(x => x.OrganizationId == organizationId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
            var run = new Inspection
            {
                OrganizationId = organizationId, VehicleId = v!.Id, Number = number, TemplateId = t!.Id, TemplateName = t.Name, Type = t.Name,
                InspectionDate = Today, Status = InspectionConstants.StatusInProgress, Odometer = r.Odometer, InspectorName = inspector, InspectorUserId = userId
            };
            await _uow.Repository<Inspection>().AddAsyncGetID(run, userId, ct);
            _db.InspectionItems.AddRange(t.Items.OrderBy(i => i.Sort).Select(i => new InspectionItem
            {
                OrganizationId = organizationId, InspectionId = run.Id, Sort = i.Sort, ChecklistItem = i.Label, PresetPartId = i.PresetPartId,
                PartName = i.PresetPart.Name, FieldType = i.FieldType, Critical = i.Critical, Unit = i.Unit, Min = i.Min, Max = i.Max,
                IssueOnFail = i.IssueOnFail, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
            }));
            await _db.SaveChangesAsync(ct);
            return await GetAsync(organizationId, run.Id, ct);
        }

        public async Task<ServiceResult<InspectionItemDto>> AnswerAsync(Guid organizationId, Guid userId, Guid inspectionId, Guid itemId, InspectionAnswerRequest r, CancellationToken ct = default)
        {
            var (item, error) = await EditableItemAsync(organizationId, inspectionId, itemId, ct);
            if (error is not null) return Fail<InspectionItemDto>(error);

            var e = new Dictionary<string, string>();
            if (r.Result is not null && !InspectionConstants.Results.Contains(r.Result)) e["result"] = "Answer must be pass, fail or N/A.";
            if (item!.FieldType == InspectionConstants.FieldScale && r.Value is not null && (r.Value < 1 || r.Value > 5 || r.Value != Math.Floor(r.Value.Value)))
                e["value"] = "Score must be a whole number from 1 to 5.";
            if (r.Comment is { Length: > 1000 }) e["comment"] = "Comment must be 1000 characters or fewer.";
            if (e.Count > 0) return ServiceResult<InspectionItemDto>.Invalid(e);

            item.Result = r.Result;
            item.Value = item.FieldType is InspectionConstants.FieldGauge or InspectionConstants.FieldScale ? r.Value : null;
            // A gauge reading outside the expected range is a failure.
            if (item.FieldType == InspectionConstants.FieldGauge && item.Value is not null && item.Result != InspectionConstants.ResultNa
                && ((item.Min is not null && item.Value < item.Min) || (item.Max is not null && item.Value > item.Max)))
                item.Result = InspectionConstants.ResultFail;
            item.Passed = item.Result == InspectionConstants.ResultPass;
            item.FailureReason = item.Result == InspectionConstants.ResultFail ? NullIfBlank(r.Comment) : null;
            await _uow.Repository<InspectionItem>().UpdateAsync(item, userId, ct);
            return ServiceResult<InspectionItemDto>.Ok(ToItem(item));
        }

        public async Task<ServiceResult<InspectionItemDto>> SetPhotoAsync(Guid organizationId, Guid userId, Guid inspectionId, Guid itemId, FileUpload file, CancellationToken ct = default)
        {
            var (item, error) = await EditableItemAsync(organizationId, inspectionId, itemId, ct);
            if (error is not null) return Fail<InspectionItemDto>(error);

            var ext = Path.GetExtension(file.FileName);
            string? message = null;
            if (file.Length <= 0) message = "The uploaded file is empty.";
            else if (file.Length > InspectionConstants.MaxPhotoBytes) message = "The photo must be 5 MB or smaller.";
            else if (!PhotoTypes.ContainsKey(ext) || !await IsImageAsync(file.Content, ext)) message = "Only JPG, PNG or WEBP images are allowed.";
            if (message is not null) return ServiceResult<InspectionItemDto>.Invalid(new Dictionary<string, string> { ["photo"] = message });

            var old = item!.PhotoKey;
            item.PhotoKey = await _storage.SaveAsync(organizationId, "inspection-photos", file.Content, ext.ToLowerInvariant(), ct);
            item.PhotoContentType = PhotoTypes[ext];
            await _uow.Repository<InspectionItem>().UpdateAsync(item, userId, ct);
            if (old is not null) await _storage.DeleteAsync(old, ct);
            return ServiceResult<InspectionItemDto>.Ok(ToItem(item));
        }

        public async Task<ServiceResult<InspectionItemDto>> RemovePhotoAsync(Guid organizationId, Guid userId, Guid inspectionId, Guid itemId, CancellationToken ct = default)
        {
            var (item, error) = await EditableItemAsync(organizationId, inspectionId, itemId, ct);
            if (error is not null) return Fail<InspectionItemDto>(error);
            if (item!.PhotoKey is null) return ServiceResult<InspectionItemDto>.Ok(ToItem(item));
            var key = item.PhotoKey;
            item.PhotoKey = null;
            item.PhotoContentType = null;
            await _uow.Repository<InspectionItem>().UpdateAsync(item, userId, ct);
            await _storage.DeleteAsync(key, ct);
            return ServiceResult<InspectionItemDto>.Ok(ToItem(item));
        }

        public async Task<ServiceResult<FileDownload>> OpenPhotoAsync(Guid organizationId, Guid inspectionId, Guid itemId, CancellationToken ct = default)
        {
            var item = await _db.InspectionItems.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == itemId && i.InspectionId == inspectionId && i.OrganizationId == organizationId && i.Inspection.CurrentState == Active, ct);
            if (item?.PhotoKey is null) return ServiceResult<FileDownload>.NotFound("Photo not found.");
            var stream = await _storage.OpenReadAsync(item.PhotoKey, ct);
            return stream is null
                ? ServiceResult<FileDownload>.NotFound("Photo not found.")
                : ServiceResult<FileDownload>.Ok(new FileDownload(stream, $"evidence{Path.GetExtension(item.PhotoKey)}", item.PhotoContentType ?? "image/jpeg"));
        }

        public async Task<ServiceResult<InspectionCompleteResult>> CompleteAsync(Guid organizationId, Guid userId, Guid inspectionId, CancellationToken ct = default)
        {
            var run = await _db.Inspections.Include(i => i.Items).Include(i => i.Vehicle)
                .FirstOrDefaultAsync(i => i.Id == inspectionId && i.OrganizationId == organizationId && i.CurrentState == Active, ct);
            if (run is null) return ServiceResult<InspectionCompleteResult>.NotFound("Inspection not found.");
            if (run.Status != InspectionConstants.StatusInProgress)
                return ServiceResult<InspectionCompleteResult>.Conflict("INSPECTION_COMPLETED", "This inspection is already completed.");

            var problems = run.Items.Where(i => Problem(i) is not null).OrderBy(i => i.Sort)
                .ToDictionary(i => $"items.{i.Id}", i => $"{i.Sort:00} {i.ChecklistItem}: {Problem(i)}");
            if (problems.Count > 0) return ServiceResult<InspectionCompleteResult>.Invalid(problems);

            var code = Code(run.Number);
            var v = run.Vehicle;
            var failed = run.Items.Where(i => i.Result == InspectionConstants.ResultFail).OrderBy(i => i.Sort).ToList();
            var criticalFailed = failed.Where(i => i.Critical).ToList();

            run.Status = InspectionConstants.StatusCompleted;
            run.InspectionDate = Today;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = userId;

            // Odometer: log a reading when the inspection moved it forward.
            var maxReading = await _db.OdometerReadings.Where(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active)
                .MaxAsync(x => (decimal?)x.Value, ct) ?? 0;
            if (run.Odometer is not null && run.Odometer > maxReading)
            {
                _db.OdometerReadings.Add(new OdometerReading
                {
                    OrganizationId = organizationId, VehicleId = v.Id, ReadingDate = Today, Value = run.Odometer.Value,
                    Source = VehicleConstants.ReadingInspection, RecordedBy = run.InspectorName,
                    CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                });
                v.CurrentOdometer = run.Odometer;
            }

            // Issues for failed checks.
            var issueNumber = await _db.VehicleIssues.Where(x => x.OrganizationId == organizationId).MaxAsync(x => (int?)x.Number, ct) ?? 0;
            var raised = 0;
            foreach (var i in failed.Where(i => i.IssueOnFail))
            {
                var issue = new VehicleIssue
                {
                    OrganizationId = organizationId, VehicleId = v.Id, Number = ++issueNumber,
                    Title = Truncate($"{i.ChecklistItem} failed inspection", 160),
                    Note = Truncate($"{code} · {(i.FailureReason ?? "No comment recorded")}{(i.Value is null ? "" : $" · reading {i.Value:0.##}{(i.Unit is null ? "" : " " + i.Unit)}")}", 1000),
                    PresetPartId = i.PresetPartId is not null && await _db.PresetParts.AnyAsync(p => p.Id == i.PresetPartId, ct) ? i.PresetPartId : null,
                    Priority = i.Critical ? VehicleConstants.PriorityHigh : VehicleConstants.PriorityMedium,
                    Source = VehicleConstants.IssueSourceInspection, Status = VehicleConstants.IssueOpen, ReportedDate = Today,
                    SourceInspectionId = run.Id, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                };
                _db.VehicleIssues.Add(issue);
                i.IssueId = issue.Id;
                raised++;
            }

            // A failed critical check grounds the vehicle and schedules the corrective job.
            Guid? maintenanceId = null;
            if (criticalFailed.Count > 0)
            {
                v.Status = VehicleConstants.StatusDown;
                v.UpdatedAt = DateTime.UtcNow;
                v.UpdatedBy = userId;
                var job = new Maintenance
                {
                    OrganizationId = organizationId, VehicleId = v.Id, Number = await WorkOrders.WorkOrderService.NextNumberAsync(_db, organizationId, ct),
                    Type = MaintenanceConstants.TypeCorrective, Priority = MaintenanceConstants.PriorityCritical, Status = MaintenanceConstants.StatusScheduled,
                    Description = Truncate($"Critical inspection failure — {code}", 500), DueDate = Today,
                    Source = MaintenanceConstants.SourceInspection, SourceRef = code, InspectionId = run.Id, OdometerAtRaise = run.Odometer ?? v.CurrentOdometer,
                    EstimatedCost = 0, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                };
                _db.MaintenanceRecords.Add(job);
                var partIds = criticalFailed.Where(i => i.PresetPartId != null).Select(i => i.PresetPartId!.Value).ToList();
                var cats = await _db.PresetParts.AsNoTracking().Where(p => partIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.CategoryId, p.Category.Name }).ToDictionaryAsync(p => p.Id, ct);
                var safety = await _db.TaskCategories.AsNoTracking()
                    .Where(c => c.OrganizationId == organizationId && c.CurrentState == Active && c.Name == "Safety")
                    .Select(c => new { c.Id, c.Name }).FirstOrDefaultAsync(ct);
                var sort = 0;
                foreach (var i in criticalFailed)
                {
                    var cat = i.PresetPartId is not null && cats.TryGetValue(i.PresetPartId.Value, out var c) ? c : null;
                    _db.MaintenanceTasks.Add(new MaintenanceTask
                    {
                        OrganizationId = organizationId, MaintenanceId = job.Id, Sort = ++sort, Description = Truncate($"Rectify {i.ChecklistItem}", 200),
                        PartCategoryId = cat?.CategoryId, PartCategoryName = cat?.Name, TaskCategoryId = safety?.Id, TaskCategoryName = safety?.Name,
                        Cost = 0, CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                    });
                }
                maintenanceId = job.Id;
            }

            await _db.SaveChangesAsync(ct);
            var detail = (await GetAsync(organizationId, run.Id, ct)).Value!;
            return ServiceResult<InspectionCompleteResult>.Ok(new InspectionCompleteResult(detail, raised, criticalFailed.Count > 0, maintenanceId));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid inspectionId, CancellationToken ct = default)
        {
            var exists = await _db.Inspections.AsNoTracking().AnyAsync(x => x.Id == inspectionId && x.OrganizationId == organizationId && x.CurrentState == Active, ct);
            if (!exists) return ServiceResult<bool>.NotFound("Inspection not found.");
            var keys = await _db.InspectionItems.AsNoTracking().Where(i => i.InspectionId == inspectionId && i.PhotoKey != null).Select(i => i.PhotoKey!).ToListAsync(ct);
            await _uow.Repository<Inspection>().DeleteAsync(inspectionId, ct);
            foreach (var k in keys) await _storage.DeleteAsync(k, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        public static string Code(int number) => $"INS-{number:00000}";

        private async Task<(InspectionItem? Item, ServiceError? Error)> EditableItemAsync(Guid organizationId, Guid inspectionId, Guid itemId, CancellationToken ct)
        {
            var status = await _db.Inspections.AsNoTracking()
                .Where(x => x.Id == inspectionId && x.OrganizationId == organizationId && x.CurrentState == Active)
                .Select(x => x.Status).FirstOrDefaultAsync(ct);
            if (status is null) return (null, new ServiceError(ServiceErrorType.NotFound, "RESOURCE_NOT_FOUND", "Inspection not found."));
            if (status != InspectionConstants.StatusInProgress)
                return (null, new ServiceError(ServiceErrorType.Conflict, "INSPECTION_COMPLETED", "A completed inspection can't be changed."));
            var item = await _uow.Repository<InspectionItem>().GetByIdAsync(itemId, ct);
            if (item is null || item.InspectionId != inspectionId || item.OrganizationId != organizationId)
                return (null, new ServiceError(ServiceErrorType.NotFound, "RESOURCE_NOT_FOUND", "Check not found."));
            return (item, null);
        }

        private static ServiceResult<T> Fail<T>(ServiceError e) => ServiceResult<T>.Fail(e);

        /// <summary>What still blocks completion for a check.</summary>
        private static string? Problem(InspectionItem i)
        {
            if (i.Result is null) return "Not answered yet.";
            if (i.Result == InspectionConstants.ResultNa) return null;
            if (i.FieldType == InspectionConstants.FieldGauge && i.Value is null) return $"Enter the reading{(i.Unit is null ? "" : $" ({i.Unit})")}.";
            if (i.FieldType == InspectionConstants.FieldScale && i.Value is null) return "Pick a score from 1 to 5.";
            if (i.Result == InspectionConstants.ResultFail && i.PhotoKey is null) return "Photo evidence required before this inspection can be completed.";
            if (i.FieldType == InspectionConstants.FieldPhoto && i.PhotoKey is null) return "Attach the photo.";
            return null;
        }

        private static InspectionSummaryDto Summarize(IReadOnlyCollection<string?> results) => new(
            results.Count, results.Count(r => r is not null), results.Count(r => r == InspectionConstants.ResultPass),
            results.Count(r => r == InspectionConstants.ResultFail), results.Count(r => r == InspectionConstants.ResultNa));

        private static InspectionItemDto ToItem(InspectionItem i) => new(
            i.Id, i.Sort, i.ChecklistItem, i.PresetPartId, i.PartName, i.FieldType, InspectionConstants.FieldLabel(i.FieldType),
            i.Critical, i.Unit, i.Min, i.Max, i.IssueOnFail, i.Result, i.Value, i.FailureReason, i.PhotoKey is not null, i.IssueId, Problem(i));

        private static InspectionDetailDto ToDetail(Inspection x)
        {
            var items = x.Items.OrderBy(i => i.Sort).Select(ToItem).ToList();
            return new InspectionDetailDto(x.Id, Code(x.Number), x.VehicleId, $"{x.Vehicle.Make} {x.Vehicle.Model}".Trim(), x.Vehicle.PlateNumber,
                x.Vehicle.ReadingUnit, x.TemplateId, x.TemplateName, x.InspectionDate, x.Odometer, x.InspectorName, x.Status, x.CompletedAtUtc,
                Summarize(items.Select(i => i.Result).ToList()),
                items.Any(i => i.Critical && i.Result == InspectionConstants.ResultFail),
                x.Status == InspectionConstants.StatusInProgress && items.All(i => i.Problem is null),
                items);
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
        private static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private static async Task<bool> IsImageAsync(Stream content, string extension)
        {
            var h = new byte[12];
            var read = await content.ReadAsync(h.AsMemory(0, h.Length));
            if (content.CanSeek) content.Position = 0;
            if (read < 12) return false;
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
                ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
                ".webp" => h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
                _ => false
            };
        }
    }
}
