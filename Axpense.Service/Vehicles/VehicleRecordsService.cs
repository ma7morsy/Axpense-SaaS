using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Data.Entities.CostEntities;
using Axpense.Data.Entities.MaintenanceRecord;
using Axpense.Data.Entities.VehicleEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Service.Common;
using Axpense.Service.Fleet;
using Axpense.Service.Settings;
using Axpense.Service.Vehicles.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Vehicles
{
    /// <summary>
    /// Profile tabs: parts, issues, fuel, odometer readings, maintenance, expenses, inspections, driver history.
    /// Rules:
    /// <list type="bullet">
    /// <item>Readings are monotonic in time: a reading must sit between its neighbours (by date); the vehicle's current value is the highest reading.</item>
    /// <item>A fuel entry with a higher odometer logs a "Fuel entry" reading. Fuel is spend on its own, so it is not duplicated as an expense.</item>
    /// <item>Replacing a part retires it and fits a new one; a covered part can be claimed under warranty (cost 0, no expense).</item>
        /// </list>
    /// </summary>
    public sealed class VehicleRecordsService : IVehicleRecordsService
    {
        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;

        public VehicleRecordsService(AxpenseDbContext db, IUnitOfWork uow)
        {
            _db = db;
            _uow = uow;
        }

        private static DateTime Today => DateTime.UtcNow.Date;
        private static int Active => (int)CurrentStatusType.Active;

        // =====================================================================
        // Parts
        // =====================================================================
        public async Task<ServiceResult<List<VehiclePartDto>>> PartsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            var v = await FindVehicleAsync(organizationId, vehicleId, ct);
            if (v is null) return ServiceResult<List<VehiclePartDto>>.NotFound("Vehicle not found.");
            var parts = await PartsQuery(organizationId, vehicleId).ToListAsync(ct);
            return ServiceResult<List<VehiclePartDto>>.Ok(parts
                .Select(p => ToDto(p, v))
                .OrderBy(p => p.Status == VehicleConstants.PartRetired ? 1 : 0)
                .ThenByDescending(p => p.Wear.Percent)
                .ToList());
        }

        public async Task<ServiceResult<VehiclePartDto>> AddPartAsync(Guid organizationId, Guid userId, Guid vehicleId, VehiclePartRequest request, CancellationToken ct = default)
        {
            var v = await FindVehicleAsync(organizationId, vehicleId, ct);
            if (v is null) return ServiceResult<VehiclePartDto>.NotFound("Vehicle not found.");
            var errors = await ValidatePartAsync(organizationId, v, request, isNew: true, ct);
            if (errors.Count > 0) return ServiceResult<VehiclePartDto>.Invalid(errors);

            var part = new VehiclePart { OrganizationId = organizationId, VehicleId = vehicleId, Number = await NextPartNumberAsync(organizationId, ct) };
            ApplyPart(part, request, v);
            await _uow.Repository<VehiclePart>().AddAsyncGetID(part, userId, ct);
            return ServiceResult<VehiclePartDto>.Ok(await PartDtoAsync(organizationId, part.Id, v, ct));
        }

        public async Task<ServiceResult<VehiclePartDto>> UpdatePartAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid partId, VehiclePartRequest request, CancellationToken ct = default)
        {
            var v = await FindVehicleAsync(organizationId, vehicleId, ct);
            if (v is null) return ServiceResult<VehiclePartDto>.NotFound("Vehicle not found.");
            var repo = _uow.Repository<VehiclePart>();
            var part = await repo.GetByIdAsync(partId, ct);
            if (part is null || part.OrganizationId != organizationId || part.VehicleId != vehicleId || part.CurrentState != Active)
                return ServiceResult<VehiclePartDto>.NotFound("Part not found.");
            var errors = await ValidatePartAsync(organizationId, v, request, isNew: false, ct);
            if (errors.Count > 0) return ServiceResult<VehiclePartDto>.Invalid(errors);

            var wasRetired = part.Status == VehicleConstants.PartRetired;
            ApplyPart(part, request, v);
            if (part.Status == VehicleConstants.PartRetired && !wasRetired) part.RetiredDate = Today;
            if (part.Status != VehicleConstants.PartRetired) part.RetiredDate = null;
            await repo.UpdateAsync(part, userId, ct);
            return ServiceResult<VehiclePartDto>.Ok(await PartDtoAsync(organizationId, part.Id, v, ct));
        }

        public async Task<ServiceResult<bool>> DeletePartAsync(Guid organizationId, Guid vehicleId, Guid partId, CancellationToken ct = default)
        {
            var exists = await _db.VehicleParts.AsNoTracking().AnyAsync(p => p.Id == partId && p.OrganizationId == organizationId && p.VehicleId == vehicleId && p.CurrentState == Active, ct);
            if (!exists) return ServiceResult<bool>.NotFound("Part not found.");
            await _uow.Repository<VehiclePart>().DeleteAsync(partId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<VehiclePartReplaceResult>> ReplacePartAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid partId, VehiclePartReplaceRequest request, CancellationToken ct = default)
        {
            var v = await FindVehicleAsync(organizationId, vehicleId, ct);
            if (v is null) return ServiceResult<VehiclePartReplaceResult>.NotFound("Vehicle not found.");
            var repo = _uow.Repository<VehiclePart>();
            var old = await repo.GetByIdAsync(partId, ct);
            if (old is null || old.OrganizationId != organizationId || old.VehicleId != vehicleId || old.CurrentState != Active)
                return ServiceResult<VehiclePartReplaceResult>.NotFound("Part not found.");
            if (old.Status == VehicleConstants.PartRetired)
                return ServiceResult<VehiclePartReplaceResult>.Conflict("PART_RETIRED", "This part is already retired.");

            var wear = PartWearCalculator.Evaluate(old, v.CurrentOdometer, v.ReadingUnit, Today);
            var claimed = request.ClaimWarranty && wear.Warranty.Covered;
            var fit = new VehiclePartRequest
            {
                PresetPartId = old.PresetPartId,
                Serial = request.Serial,
                UnitCost = claimed ? 0 : request.UnitCost,
                LifespanKm = request.LifespanKm,
                LifespanMonths = request.LifespanMonths,
                InstalledReading = request.InstalledReading ?? v.CurrentOdometer ?? 0,
                InstalledDate = request.InstalledDate ?? Today,
                WarrantyUntil = request.WarrantyUntil,
                WarrantyKm = request.WarrantyKm,
                Status = VehicleConstants.PartInService
            };
            var errors = await ValidatePartAsync(organizationId, v, fit, isNew: true, ct);
            if (string.IsNullOrWhiteSpace(request.Serial)) errors["serial"] = "Serial number of the new part is required.";
            if (fit.InstalledDate < old.InstalledDate) errors["installedDate"] = "The new part can't be activated before the old one.";
            if (errors.Count > 0) return ServiceResult<VehiclePartReplaceResult>.Invalid(errors);

            var partName = await _db.PresetParts.AsNoTracking().Where(p => p.Id == old.PresetPartId).Select(p => p.Name).FirstAsync(ct);
            var unit = v.ReadingUnit;

            old.Status = VehicleConstants.PartRetired;
            old.RetiredDate = fit.InstalledDate!.Value.Date;
            old.Notes = Append(old.Notes, claimed
                ? $"Warranty claim — replaced free after {wear.DistanceRun:N0} {unit}"
                : $"Replaced after {wear.DistanceRun:N0} {unit}");
            await repo.UpdateAsync(old, userId, ct);

            var fresh = new VehiclePart { OrganizationId = organizationId, VehicleId = vehicleId, Number = await NextPartNumberAsync(organizationId, ct) };
            ApplyPart(fresh, fit, v);
            fresh.Notes = $"Replaces {VehicleService.PartCode(old.Number)} ({old.Serial ?? "no serial"}){(claimed ? " — supplied free under warranty" : "")}";
            await repo.AddAsyncGetID(fresh, userId, ct);

            Guid? maintenanceId = null, expenseId = null;
            var cost = fresh.UnitCost ?? 0;
            if (request.RaiseWorkOrder)
            {
                var category = await _db.PresetParts.AsNoTracking().Where(p => p.Id == old.PresetPartId)
                    .Select(p => new { p.CategoryId, p.Category.Name }).FirstAsync(ct);
                var job = new Maintenance
                {
                    OrganizationId = organizationId, VehicleId = vehicleId, Number = await WorkOrders.WorkOrderService.NextNumberAsync(_db, organizationId, ct),
                    Type = MaintenanceConstants.TypeCorrective, Priority = MaintenanceConstants.PriorityMedium, Status = MaintenanceConstants.StatusScheduled,
                    Description = $"Replace {partName}{(claimed ? " (warranty claim)" : "")}", DueDate = fresh.InstalledDate, EstimatedCost = cost,
                    Source = MaintenanceConstants.SourcePart, SourceRef = VehicleService.PartCode(old.Number), OdometerAtRaise = v.CurrentOdometer,
                    CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                };
                _db.MaintenanceRecords.Add(job);
                _db.MaintenanceTasks.Add(new MaintenanceTask
                {
                    OrganizationId = organizationId, MaintenanceId = job.Id, Sort = 1, Description = $"Fit new {partName} ({fresh.Serial})",
                    PartCategoryId = category.CategoryId, PartCategoryName = category.Name, Cost = cost,
                    CreatedBy = userId, CreatedAt = DateTime.UtcNow, CurrentState = Active
                });
                await _db.SaveChangesAsync(ct);
                maintenanceId = job.Id;
            }
            if (request.RecordExpense && !claimed && cost > 0)
            {
                var type = await PartsExpenseTypeNameAsync(organizationId, ct);
                var expense = new Expense
                {
                    OrganizationId = organizationId, VehicleId = vehicleId, Category = type,
                    Description = $"{partName} — {fresh.Serial}", Amount = cost, ExpenseDate = fresh.InstalledDate,
                    Notes = $"Replaces {VehicleService.PartCode(old.Number)}"
                };
                await _uow.Repository<Expense>().AddAsyncGetID(expense, userId, ct);
                expenseId = expense.Id;
            }

            return ServiceResult<VehiclePartReplaceResult>.Ok(new VehiclePartReplaceResult(
                await PartDtoAsync(organizationId, old.Id, v, ct), await PartDtoAsync(organizationId, fresh.Id, v, ct), claimed, maintenanceId, expenseId));
        }

        // =====================================================================
        // Issues
        // =====================================================================
        public async Task<ServiceResult<List<VehicleIssueDto>>> IssuesAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            if (await FindVehicleAsync(organizationId, vehicleId, ct) is null) return ServiceResult<List<VehicleIssueDto>>.NotFound("Vehicle not found.");
            var rows = await IssuesQuery(organizationId, vehicleId).ToListAsync(ct);
            return ServiceResult<List<VehicleIssueDto>>.Ok(rows
                .Select(Pad)
                .OrderBy(i => i.Status == VehicleConstants.IssueResolved ? 1 : 0)
                .ThenByDescending(i => i.ReportedDate).ThenByDescending(i => i.Code)
                .ToList());
        }

        public async Task<ServiceResult<VehicleIssueDto>> AddIssueAsync(Guid organizationId, Guid userId, Guid vehicleId, VehicleIssueRequest request, CancellationToken ct = default)
        {
            if (await FindVehicleAsync(organizationId, vehicleId, ct) is null) return ServiceResult<VehicleIssueDto>.NotFound("Vehicle not found.");
            var e = new Dictionary<string, string>();
            var title = request.Title?.Trim() ?? "";
            if (title.Length == 0) e["title"] = "Describe the issue.";
            else if (title.Length > 160) e["title"] = "Title must be 160 characters or fewer.";
            if (request.Note is { Length: > 1000 }) e["note"] = "Note must be 1000 characters or fewer.";
            var priority = request.Priority ?? VehicleConstants.PriorityMedium;
            if (!VehicleConstants.Priorities.Contains(priority)) e["priority"] = $"Priority must be one of: {string.Join(", ", VehicleConstants.Priorities)}.";
            var source = request.Source ?? VehicleConstants.IssueSourceManual;
            if (!VehicleConstants.IssueSources.Contains(source)) e["source"] = $"Source must be one of: {string.Join(", ", VehicleConstants.IssueSources)}.";
            if (request.ReportedDate?.Date > Today) e["reportedDate"] = "Reported date can't be in the future.";
            if (request.PresetPartId is not null && !await PresetPartExistsAsync(organizationId, request.PresetPartId.Value, ct)) e["presetPartId"] = "Part not found in the catalogue.";
            if (e.Count > 0) return ServiceResult<VehicleIssueDto>.Invalid(e);

            var next = (await _db.VehicleIssues.Where(x => x.OrganizationId == organizationId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
            var issue = new VehicleIssue
            {
                OrganizationId = organizationId, VehicleId = vehicleId, Number = next, Title = title,
                Note = NullIfBlank(request.Note), PresetPartId = request.PresetPartId, Priority = priority, Source = source,
                Status = VehicleConstants.IssueOpen, ReportedDate = (request.ReportedDate ?? Today).Date
            };
            await _uow.Repository<VehicleIssue>().AddAsyncGetID(issue, userId, ct);
            return ServiceResult<VehicleIssueDto>.Ok(Pad(await IssuesQuery(organizationId, vehicleId, issue.Id).FirstAsync(ct)));
        }

        public async Task<ServiceResult<VehicleIssueDto>> SetIssueStatusAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid issueId, VehicleIssueStatusRequest request, CancellationToken ct = default)
        {
            if (!VehicleConstants.IssueStatuses.Contains(request.Status))
                return ServiceResult<VehicleIssueDto>.Invalid(new Dictionary<string, string> { ["status"] = $"Status must be one of: {string.Join(", ", VehicleConstants.IssueStatuses)}." });
            var repo = _uow.Repository<VehicleIssue>();
            var issue = await repo.GetByIdAsync(issueId, ct);
            if (issue is null || issue.OrganizationId != organizationId || issue.VehicleId != vehicleId || issue.CurrentState != Active)
                return ServiceResult<VehicleIssueDto>.NotFound("Issue not found.");
            issue.Status = request.Status;
            issue.ResolvedDate = request.Status == VehicleConstants.IssueResolved ? Today : null;
            await repo.UpdateAsync(issue, userId, ct);
            return ServiceResult<VehicleIssueDto>.Ok(Pad(await IssuesQuery(organizationId, vehicleId, issueId).FirstAsync(ct)));
        }

        public async Task<ServiceResult<bool>> DeleteIssueAsync(Guid organizationId, Guid vehicleId, Guid issueId, CancellationToken ct = default)
        {
            var exists = await _db.VehicleIssues.AsNoTracking().AnyAsync(x => x.Id == issueId && x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active, ct);
            if (!exists) return ServiceResult<bool>.NotFound("Issue not found.");
            await _uow.Repository<VehicleIssue>().DeleteAsync(issueId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================================
        // Fuel
        // =====================================================================
        public async Task<ServiceResult<List<FuelRecordDto>>> FuelAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            var v = await FindVehicleAsync(organizationId, vehicleId, ct);
            if (v is null) return ServiceResult<List<FuelRecordDto>>.NotFound("Vehicle not found.");
            var rows = await _db.FuelTransactions.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active)
                .OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Odometer)
                .ToListAsync(ct);
            var unit = VehicleService.TankUnit(v);
            var list = rows.Select((r, i) =>
            {
                // Consumption over the distance since the previous fill-up (by odometer).
                var prev = rows.Skip(i + 1).FirstOrDefault(p => p.Odometer is not null);
                var dist = r.Odometer is not null && prev?.Odometer is not null ? r.Odometer - prev.Odometer : null;
                decimal? cons = dist is > 0 && v.ReadingUnit == VehicleConstants.UnitKm ? Math.Round(r.QuantityLiters / dist.Value * 100, 1) : null;
                return new FuelRecordDto(r.Id, r.TransactionDate, r.Station, r.QuantityLiters, unit, r.UnitPrice, r.TotalAmount, r.Odometer, cons);
            }).ToList();
            return ServiceResult<List<FuelRecordDto>>.Ok(list);
        }

        public async Task<ServiceResult<FuelRecordDto>> AddFuelAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, FuelRecordRequest request, CancellationToken ct = default)
        {
            var v = await FindVehicleAsync(organizationId, vehicleId, ct);
            if (v is null) return ServiceResult<FuelRecordDto>.NotFound("Vehicle not found.");
            var e = new Dictionary<string, string>();
            if (request.Date is null) e["date"] = "Date is required.";
            else if (request.Date.Value.Date > Today) e["date"] = "Date can't be in the future.";
            var station = request.Station?.Trim() ?? "";
            if (station.Length == 0) e["station"] = "Station or depot is required.";
            else if (station.Length > 120) e["station"] = "Station must be 120 characters or fewer.";
            if (request.Quantity is null or <= 0) e["quantity"] = "Quantity must be greater than 0.";
            else if (v.TankCapacity is not null && request.Quantity > v.TankCapacity * 1.05m) e["quantity"] = $"Quantity is above the tank capacity ({v.TankCapacity:N0} {VehicleService.TankUnit(v)}).";
            if (request.TotalCost is null or <= 0) e["totalCost"] = "Total cost must be greater than 0.";
            if (request.Odometer is null) e["odometer"] = "Odometer reading is required.";
            else if (request.Odometer < 0) e["odometer"] = "Odometer can't be negative.";
            if (e.Count == 0)
            {
                var bounds = await ReadingBoundsAsync(organizationId, vehicleId, request.Date!.Value.Date, null, ct);
                var err = BoundsError(bounds, request.Odometer!.Value, v.ReadingUnit);
                if (err is not null) e["odometer"] = err;
            }
            if (e.Count > 0) return ServiceResult<FuelRecordDto>.Invalid(e);

            var fuel = new FuelTransaction
            {
                OrganizationId = organizationId, VehicleId = vehicleId, TransactionDate = request.Date!.Value.Date,
                QuantityLiters = request.Quantity!.Value, TotalAmount = request.TotalCost!.Value,
                UnitPrice = Math.Round(request.TotalCost.Value / request.Quantity.Value, 3),
                Odometer = request.Odometer, FuelType = v.FuelType, Station = station
            };
            await _uow.Repository<FuelTransaction>().AddAsyncGetID(fuel, userId, ct);
            if (request.Odometer > (v.CurrentOdometer ?? 0))
                await AddReadingInternalAsync(organizationId, userId, vehicleId, fuel.TransactionDate, request.Odometer!.Value, VehicleConstants.ReadingFuel, userName, ct);

            var list = await FuelAsync(organizationId, vehicleId, ct);
            return ServiceResult<FuelRecordDto>.Ok(list.Value!.First(x => x.Id == fuel.Id));
        }

        public async Task<ServiceResult<bool>> DeleteFuelAsync(Guid organizationId, Guid vehicleId, Guid fuelId, CancellationToken ct = default)
        {
            var exists = await _db.FuelTransactions.AsNoTracking().AnyAsync(x => x.Id == fuelId && x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active, ct);
            if (!exists) return ServiceResult<bool>.NotFound("Fuel entry not found.");
            await _uow.Repository<FuelTransaction>().DeleteAsync(fuelId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================================
        // Odometer readings
        // =====================================================================
        public async Task<ServiceResult<List<OdometerReadingDto>>> ReadingsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            if (await FindVehicleAsync(organizationId, vehicleId, ct) is null) return ServiceResult<List<OdometerReadingDto>>.NotFound("Vehicle not found.");
            var rows = await ReadingsQuery(organizationId, vehicleId).OrderByDescending(x => x.ReadingDate).ThenByDescending(x => x.Value).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
            return ServiceResult<List<OdometerReadingDto>>.Ok(rows.Select((r, i) =>
            {
                var prev = rows.ElementAtOrDefault(i + 1);
                return new OdometerReadingDto(r.Id, r.ReadingDate, r.Value, prev is null ? null : r.Value - prev.Value, r.Source, r.RecordedBy);
            }).ToList());
        }

        public async Task<ServiceResult<OdometerReadingDto>> AddReadingAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, OdometerReadingRequest request, CancellationToken ct = default)
        {
            var v = await FindVehicleAsync(organizationId, vehicleId, ct);
            if (v is null) return ServiceResult<OdometerReadingDto>.NotFound("Vehicle not found.");
            var e = new Dictionary<string, string>();
            if (request.Date is null) e["date"] = "Date is required.";
            else if (request.Date.Value.Date > Today) e["date"] = "Date can't be in the future.";
            if (request.Value is null) e["value"] = "Reading is required.";
            else if (request.Value < 0) e["value"] = "Reading can't be negative.";
            var source = string.IsNullOrWhiteSpace(request.Source) ? VehicleConstants.ReadingManual : request.Source!;
            if (!VehicleConstants.ManualReadingSources.Contains(source)) e["source"] = $"Source must be one of: {string.Join(", ", VehicleConstants.ManualReadingSources)}.";
            if (request.RecordedBy is { Length: > 120 }) e["recordedBy"] = "Recorded by must be 120 characters or fewer.";
            if (e.Count == 0)
            {
                var err = BoundsError(await ReadingBoundsAsync(organizationId, vehicleId, request.Date!.Value.Date, null, ct), request.Value!.Value, v.ReadingUnit);
                if (err is not null) e["value"] = err;
            }
            if (e.Count > 0) return ServiceResult<OdometerReadingDto>.Invalid(e);

            var id = await AddReadingInternalAsync(organizationId, userId, vehicleId, request.Date!.Value.Date, request.Value!.Value, source,
                string.IsNullOrWhiteSpace(request.RecordedBy) ? userName : request.RecordedBy!, ct);
            var list = await ReadingsAsync(organizationId, vehicleId, ct);
            return ServiceResult<OdometerReadingDto>.Ok(list.Value!.First(x => x.Id == id));
        }

        public async Task<ServiceResult<bool>> DeleteReadingAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid readingId, CancellationToken ct = default)
        {
            var readings = await ReadingsQuery(organizationId, vehicleId).ToListAsync(ct);
            if (!readings.Any(r => r.Id == readingId)) return ServiceResult<bool>.NotFound("Reading not found.");
            if (readings.Count == 1)
                return ServiceResult<bool>.Conflict("LAST_READING", "A vehicle keeps at least one reading. Add the corrected reading first, then remove this one.");
            await _uow.Repository<OdometerReading>().DeleteAsync(readingId, ct);
            await SyncCurrentOdometerAsync(organizationId, userId, vehicleId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================================
        // Expenses
        // =====================================================================
        public async Task<ServiceResult<List<VehicleExpenseDto>>> ExpensesAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            if (await FindVehicleAsync(organizationId, vehicleId, ct) is null) return ServiceResult<List<VehicleExpenseDto>>.NotFound("Vehicle not found.");
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var rows = await _db.Expenses.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active)
                .OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
            return ServiceResult<List<VehicleExpenseDto>>.Ok(rows.Select(x => ToDto(x, types)).ToList());
        }

        public async Task<ServiceResult<VehicleExpenseDto>> AddExpenseAsync(Guid organizationId, Guid userId, Guid vehicleId, VehicleExpenseRequest request, CancellationToken ct = default)
        {
            if (await FindVehicleAsync(organizationId, vehicleId, ct) is null) return ServiceResult<VehicleExpenseDto>.NotFound("Vehicle not found.");
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var e = ValidateExpense(request, types);
            if (e.Count > 0) return ServiceResult<VehicleExpenseDto>.Invalid(e);
            var x = new Expense { OrganizationId = organizationId, VehicleId = vehicleId };
            ApplyExpense(x, request, types);
            await _uow.Repository<Expense>().AddAsyncGetID(x, userId, ct);
            return ServiceResult<VehicleExpenseDto>.Ok(ToDto(x, types));
        }

        public async Task<ServiceResult<VehicleExpenseDto>> UpdateExpenseAsync(Guid organizationId, Guid userId, Guid vehicleId, Guid expenseId, VehicleExpenseRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<Expense>();
            var x = await repo.GetByIdAsync(expenseId, ct);
            if (x is null || x.OrganizationId != organizationId || x.VehicleId != vehicleId || x.CurrentState != Active)
                return ServiceResult<VehicleExpenseDto>.NotFound("Expense not found.");
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var e = ValidateExpense(request, types);
            if (e.Count > 0) return ServiceResult<VehicleExpenseDto>.Invalid(e);
            ApplyExpense(x, request, types);
            await repo.UpdateAsync(x, userId, ct);
            return ServiceResult<VehicleExpenseDto>.Ok(ToDto(x, types));
        }

        public async Task<ServiceResult<bool>> DeleteExpenseAsync(Guid organizationId, Guid vehicleId, Guid expenseId, CancellationToken ct = default)
        {
            var exists = await _db.Expenses.AsNoTracking().AnyAsync(x => x.Id == expenseId && x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active, ct);
            if (!exists) return ServiceResult<bool>.NotFound("Expense not found.");
            await _uow.Repository<Expense>().DeleteAsync(expenseId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================================
        // Inspections / driver history
        // =====================================================================
        public async Task<ServiceResult<List<VehicleInspectionDto>>> InspectionsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            if (await FindVehicleAsync(organizationId, vehicleId, ct) is null) return ServiceResult<List<VehicleInspectionDto>>.NotFound("Vehicle not found.");
            var rows = await _db.Inspections.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active)
                .OrderByDescending(x => x.Status == InspectionConstants.StatusInProgress).ThenByDescending(x => x.InspectionDate).ThenByDescending(x => x.Number)
                .Select(x => new VehicleInspectionDto(x.Id, "INS-" + x.Number, x.TemplateName, x.InspectionDate, x.Odometer, x.InspectorName, x.Status,
                    x.Items.Count, x.Items.Count(i => i.Result == InspectionConstants.ResultPass), x.Items.Count(i => i.Result == InspectionConstants.ResultFail),
                    x.Items.Count(i => i.Result == InspectionConstants.ResultNa)))
                .ToListAsync(ct);
            rows = rows.Select(r => r with { Code = Inspections.InspectionService.Code(int.Parse(r.Code[4..])) }).ToList();
            return ServiceResult<List<VehicleInspectionDto>>.Ok(rows);
        }

        public async Task<ServiceResult<List<VehicleAssignmentDto>>> AssignmentsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            if (await FindVehicleAsync(organizationId, vehicleId, ct) is null) return ServiceResult<List<VehicleAssignmentDto>>.NotFound("Vehicle not found.");
            var today = Today;
            var rows = await _db.VehicleAssignments.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active)
                .OrderByDescending(x => x.StartDate)
                .Select(x => new { x.Id, x.DriverId, x.Driver.FullName, x.Driver.LicenseNumber, x.StartDate, x.EndDate, x.Notes })
                .ToListAsync(ct);
            return ServiceResult<List<VehicleAssignmentDto>>.Ok(rows.Select(x => new VehicleAssignmentDto(
                x.Id, x.DriverId, x.FullName, x.LicenseNumber, x.StartDate, x.EndDate, x.Notes,
                x.StartDate.Date <= today && (x.EndDate == null || x.EndDate.Value.Date > today))).ToList());
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private Task<Vehicle?> FindVehicleAsync(Guid organizationId, Guid vehicleId, CancellationToken ct) =>
            _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId && v.OrganizationId == organizationId && v.CurrentState == Active, ct);

        private IQueryable<OdometerReading> ReadingsQuery(Guid organizationId, Guid vehicleId) =>
            _db.OdometerReadings.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active);

        private sealed record Bounds(decimal? Min, DateTime? MinDate, decimal? Max, DateTime? MaxDate);

        /// <summary>A reading dated D must be ≥ every reading on or before D and ≤ every reading after D.</summary>
        private async Task<Bounds> ReadingBoundsAsync(Guid organizationId, Guid vehicleId, DateTime date, Guid? exceptId, CancellationToken ct)
        {
            var rows = await ReadingsQuery(organizationId, vehicleId).Where(x => x.Id != exceptId).Select(x => new { x.ReadingDate, x.Value }).ToListAsync(ct);
            var before = rows.Where(r => r.ReadingDate.Date <= date).OrderByDescending(r => r.Value).FirstOrDefault();
            var after = rows.Where(r => r.ReadingDate.Date > date).OrderBy(r => r.Value).FirstOrDefault();
            return new Bounds(before?.Value, before?.ReadingDate, after?.Value, after?.ReadingDate);
        }

        private static string? BoundsError(Bounds b, decimal value, string unit)
        {
            if (b.Min is not null && value < b.Min) return $"Reading can't be below {b.Min:N0} {unit} (recorded {b.MinDate:dd MMM yyyy}).";
            if (b.Max is not null && value > b.Max) return $"Reading can't be above {b.Max:N0} {unit} (recorded later, {b.MaxDate:dd MMM yyyy}).";
            return null;
        }

        private async Task<Guid> AddReadingInternalAsync(Guid organizationId, Guid userId, Guid vehicleId, DateTime date, decimal value, string source, string? by, CancellationToken ct)
        {
            var reading = new OdometerReading
            {
                OrganizationId = organizationId, VehicleId = vehicleId, ReadingDate = date, Value = value,
                Source = source, RecordedBy = NullIfBlank(by)
            };
            await _uow.Repository<OdometerReading>().AddAsyncGetID(reading, userId, ct);
            await SyncCurrentOdometerAsync(organizationId, userId, vehicleId, ct);
            return reading.Id;
        }

        /// <summary>The vehicle's current value is its highest reading.</summary>
        private async Task SyncCurrentOdometerAsync(Guid organizationId, Guid userId, Guid vehicleId, CancellationToken ct)
        {
            var max = await ReadingsQuery(organizationId, vehicleId).MaxAsync(x => (decimal?)x.Value, ct);
            var repo = _uow.Repository<Vehicle>();
            var v = await repo.GetByIdAsync(vehicleId, ct);
            if (v is null || v.CurrentOdometer == max) return;
            v.CurrentOdometer = max;
            await repo.UpdateAsync(v, userId, ct);
        }

        private IQueryable<VehiclePart> PartsQuery(Guid organizationId, Guid vehicleId) =>
            _db.VehicleParts.AsNoTracking().Include(p => p.PresetPart).ThenInclude(p => p.Category)
                .Where(p => p.OrganizationId == organizationId && p.VehicleId == vehicleId && p.CurrentState == Active);

        private async Task<VehiclePartDto> PartDtoAsync(Guid organizationId, Guid partId, Vehicle v, CancellationToken ct) =>
            ToDto(await PartsQuery(organizationId, v.Id).FirstAsync(p => p.Id == partId, ct), v);

        private static VehiclePartDto ToDto(VehiclePart p, Vehicle v) => new(
            p.Id, VehicleService.PartCode(p.Number), p.PresetPartId, p.PresetPart.Name, p.PresetPart.NameAr, p.PresetPart.Code,
            p.PresetPart.CategoryId, p.PresetPart.Category.Name,
            p.Serial, p.UnitCost, p.LifespanKm, p.LifespanMonths, p.InstalledReading, p.InstalledDate, p.WarrantyUntil, p.WarrantyKm,
            p.Status, p.RetiredDate, p.Notes, PartWearCalculator.Evaluate(p, v.CurrentOdometer, v.ReadingUnit, Today));

        private async Task<int> NextPartNumberAsync(Guid organizationId, CancellationToken ct) =>
            (await _db.VehicleParts.Where(x => x.OrganizationId == organizationId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;

        private Task<bool> PresetPartExistsAsync(Guid organizationId, Guid presetPartId, CancellationToken ct) =>
            _db.PresetParts.AsNoTracking().AnyAsync(p => p.Id == presetPartId && p.OrganizationId == organizationId && p.CurrentState == Active, ct);

        private async Task<Dictionary<string, string>> ValidatePartAsync(Guid organizationId, Vehicle v, VehiclePartRequest r, bool isNew, CancellationToken ct)
        {
            var e = new Dictionary<string, string>();
            if (r.PresetPartId is null) e["presetPartId"] = "Pick the part from the catalogue.";
            else if (!await PresetPartExistsAsync(organizationId, r.PresetPartId.Value, ct)) e["presetPartId"] = "Part not found in the catalogue.";
            if (r.Serial is { Length: > 60 }) e["serial"] = "Serial must be 60 characters or fewer.";
            if (r.UnitCost is < 0) e["unitCost"] = "Unit cost can't be negative.";
            if (r.LifespanKm is <= 0) e["lifespanKm"] = "Lifespan must be greater than 0.";
            if (r.LifespanMonths is <= 0) e["lifespanMonths"] = "Lifespan must be greater than 0.";
            if (r.LifespanKm is not null && r.LifespanMonths is not null) e["lifespanMonths"] = "Use either a distance or a months lifespan, not both.";
            if (r.InstalledReading is null) e["installedReading"] = "Activation reading is required.";
            else if (r.InstalledReading < 0) e["installedReading"] = "Activation reading can't be negative.";
            else if (r.InstalledReading > (v.CurrentOdometer ?? 0)) e["installedReading"] = $"Activation reading can't be above the current odometer ({v.CurrentOdometer ?? 0:N0} {v.ReadingUnit}).";
            if (r.InstalledDate is null) e["installedDate"] = "Activation date is required.";
            else if (r.InstalledDate.Value.Date > Today) e["installedDate"] = "Activation date can't be in the future.";
            if (r.WarrantyUntil is not null && r.InstalledDate is not null && r.WarrantyUntil.Value.Date < r.InstalledDate.Value.Date) e["warrantyUntil"] = "Warranty can't end before activation.";
            if (r.WarrantyKm is <= 0) e["warrantyKm"] = "Warranty distance must be greater than 0.";
            var status = r.Status ?? VehicleConstants.PartInService;
            if (!VehicleConstants.PartStatuses.Contains(status)) e["status"] = $"Status must be one of: {string.Join(", ", VehicleConstants.PartStatuses)}.";
            else if (isNew && status == VehicleConstants.PartRetired) e["status"] = "A new part can't be added as retired.";
            if (r.Notes is { Length: > 500 }) e["notes"] = "Notes must be 500 characters or fewer.";
            return e;
        }

        private static void ApplyPart(VehiclePart p, VehiclePartRequest r, Vehicle v)
        {
            p.PresetPartId = r.PresetPartId!.Value;
            p.Serial = NullIfBlank(r.Serial);
            p.UnitCost = r.UnitCost;
            p.LifespanKm = r.LifespanKm;
            p.LifespanMonths = r.LifespanMonths;
            p.InstalledReading = r.InstalledReading!.Value;
            p.InstalledDate = r.InstalledDate!.Value.Date;
            p.WarrantyUntil = r.WarrantyUntil?.Date;
            p.WarrantyKm = r.WarrantyKm;
            p.Status = r.Status ?? VehicleConstants.PartInService;
            p.Notes = NullIfBlank(r.Notes);
        }

        private async Task<string> PartsExpenseTypeNameAsync(Guid organizationId, CancellationToken ct)
        {
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            return types.FirstOrDefault(t => t.Name.Equals("Parts", StringComparison.OrdinalIgnoreCase))?.Name
                   ?? types.FirstOrDefault(t => t.SystemKey == SettingsDefaults.SystemOther)?.Name
                   ?? "Other";
        }

        private IQueryable<VehicleIssueDto> IssuesQuery(Guid organizationId, Guid vehicleId, Guid? issueId = null) =>
            _db.VehicleIssues.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active && (issueId == null || x.Id == issueId))
                .Select(x => new VehicleIssueDto(x.Id, "ISS-" + x.Number, x.Title, x.Note, x.PresetPartId,
                    x.PresetPart != null ? x.PresetPart.Name : null, x.Priority, x.Source, x.Status, x.ReportedDate, x.ResolvedDate));

        /// <summary>ISS-7 → ISS-0007 (formatted in memory; the query only concatenates the number).</summary>
        private static VehicleIssueDto Pad(VehicleIssueDto i) => i with { Code = "ISS-" + i.Code[4..].PadLeft(4, '0') };

        private static Dictionary<string, string> ValidateExpense(VehicleExpenseRequest r, List<Data.Entities.SettingsEntities.ExpenseType> types)
        {
            var e = new Dictionary<string, string>();
            var title = r.Title?.Trim() ?? "";
            if (title.Length == 0) e["title"] = "Title is required.";
            else if (title.Length > 200) e["title"] = "Title must be 200 characters or fewer.";
            if (r.ExpenseTypeId is null) e["expenseTypeId"] = "Pick the expense type.";
            else if (types.All(t => t.Id != r.ExpenseTypeId)) e["expenseTypeId"] = "Expense type not found.";
            if (r.Amount is null or <= 0) e["amount"] = "Amount must be greater than 0.";
            if (r.Date is null) e["date"] = "Issue date is required.";
            if (r.Note is { Length: > 500 }) e["note"] = "Note must be 500 characters or fewer.";
            return e;
        }

        private static void ApplyExpense(Expense x, VehicleExpenseRequest r, List<Data.Entities.SettingsEntities.ExpenseType> types)
        {
            x.Description = r.Title.Trim();
            x.Category = types.First(t => t.Id == r.ExpenseTypeId).Name;
            x.Amount = r.Amount!.Value;
            x.ExpenseDate = r.Date!.Value.Date;
            x.Notes = NullIfBlank(r.Note);
        }

        private static VehicleExpenseDto ToDto(Expense x, List<Data.Entities.SettingsEntities.ExpenseType> types)
        {
            var type = types.FirstOrDefault(t => t.Name.Equals(x.Category, StringComparison.OrdinalIgnoreCase));
            return new VehicleExpenseDto(x.Id, x.ExpenseDate, x.Description, type?.Id, type?.Name ?? x.Category, type?.Color, x.Notes, x.Amount);
        }

        private static string Append(string? notes, string line) => string.IsNullOrWhiteSpace(notes) ? line : $"{notes.Trim()}\n{line}";
        private static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
