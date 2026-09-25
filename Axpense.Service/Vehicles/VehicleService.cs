using System.Text.RegularExpressions;
using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Data.Entities.DriverEntities;
using Axpense.Data.Entities.VehicleEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Service.Common;
using Axpense.Service.Drivers.Dtos;
using Axpense.Service.Fleet;
using Axpense.Service.Settings;
using Axpense.Service.Vehicles.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Vehicles
{
    /// <summary>
    /// Vehicle register and profile. Business rules:
    /// <list type="bullet">
    /// <item>VIN is 17 characters (letters and digits, no I/O/Q) and unique in the organization; the plate is unique too.</item>
    /// <item>The PM trigger decides which intervals are required (usage → distance or engine hours, time → days, hours → engine hours, hybrid → days and distance/hours).</item>
    /// <item>Every change of the odometer goes through a reading (<see cref="OdometerReading"/>); the current value never goes backwards.</item>
    /// <item>A driver holds one vehicle at a time; hand-off ends the current assignment on the effective date.</item>
    /// </list>
    /// </summary>
    public sealed class VehicleService : IVehicleService
    {
        private const long MaxPhotoBytes = 5 * 1024 * 1024;
        private static readonly Dictionary<string, string> PhotoTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
        };
        private static readonly Regex VinPattern = new("^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.Compiled);

        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;
        private readonly IFileStorage _storage;
        private readonly Inspections.IInspectionService _inspections;
        private readonly Inspections.IInspectionTemplateService _templates;

        public VehicleService(AxpenseDbContext db, IUnitOfWork uow, IFileStorage storage,
            Inspections.IInspectionService inspections, Inspections.IInspectionTemplateService templates)
        {
            _db = db;
            _uow = uow;
            _storage = storage;
            _inspections = inspections;
            _templates = templates;
        }

        private static DateTime Today => DateTime.UtcNow.Date;
        private static int Active => (int)CurrentStatusType.Active;

        public VehicleOptionsDto Options() => new(
            VehicleConstants.Statuses, VehicleConstants.Categories, VehicleConstants.OwnerTypes, VehicleConstants.ReadingUnits,
            VehicleConstants.EngineTypes, VehicleConstants.FuelTypes, VehicleConstants.PmTriggers, VehicleConstants.PartStatuses,
            VehicleConstants.Priorities, VehicleConstants.IssueStatuses, VehicleConstants.IssueSources, VehicleConstants.ManualReadingSources,
            MaintenanceConstants.Types, VehicleConstants.RenewalReminderDays);

        // =====================================================================
        // Register
        // =====================================================================
        public async Task<VehicleListResponse> ListAsync(Guid organizationId, VehicleListQuery query, CancellationToken ct = default)
        {
            var vehicles = await ActiveVehicles(organizationId).OrderBy(v => v.Make).ThenBy(v => v.Model).ThenBy(v => v.PlateNumber).ToListAsync(ct);
            var ids = vehicles.Select(v => v.Id).ToList();
            var drivers = await FleetQueries.CurrentDriversAsync(_db, organizationId, ids, ct);
            var rules = await PmRules.LoadAsync(_db, organizationId, ct);
            var parts = await PartWearByVehicleAsync(organizationId, null, vehicles, ct);
            var today = Today;

            var rows = vehicles.Select(v =>
            {
                var pm = PmCalculator.Evaluate(v, today, rules);
                var blocked = PmCalculator.DispatchBlocked(v, pm, rules);
                var alerts = VehicleAlerts.For(v, pm, blocked, rules, parts.GetValueOrDefault(v.Id) ?? [], today);
                var d = drivers.GetValueOrDefault(v.Id);
                return new VehicleListItemDto(v.Id, Name(v), v.Make, v.Model, v.ModelYear, v.Category, v.OwnerType, v.OwnerName,
                    v.Vin, v.PlateNumber, v.Status, v.FuelType, v.CurrentOdometer, v.ReadingUnit, v.PhotoKey is not null,
                    pm, blocked, ToDriver(d), alerts.Count);
            }).ToList();

            IEnumerable<VehicleListItemDto> q = rows;
            if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(r => r.Status == query.Status);
            if (!string.IsNullOrWhiteSpace(query.Category)) q = q.Where(r => r.Category == query.Category);
            if (!string.IsNullOrWhiteSpace(query.Owner)) q = q.Where(r => r.OwnerType == query.Owner);
            if (!string.IsNullOrWhiteSpace(query.Pm)) q = q.Where(r => r.Pm.Status == query.Pm);
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = query.Q.Trim();
                q = q.Where(r => Contains(r.PlateNumber, term) || Contains(r.Vin, term) || Contains(r.Make, term) ||
                                 Contains(r.Model, term) || Contains(r.CurrentDriver?.FullName, term) ||
                                 Contains($"{r.Make} {r.Model}", term));
            }
            return new VehicleListResponse(q.ToList(), rows.Count);
        }

        // =====================================================================
        // Profile
        // =====================================================================
        public async Task<ServiceResult<VehicleDetailDto>> GetAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            var v = await ActiveVehicles(organizationId).FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
            if (v is null) return ServiceResult<VehicleDetailDto>.NotFound("Vehicle not found.");

            var today = Today;
            var rules = await PmRules.LoadAsync(_db, organizationId, ct);
            var pm = PmCalculator.Evaluate(v, today, rules);
            var blocked = PmCalculator.DispatchBlocked(v, pm, rules);
            var parts = (await PartWearByVehicleAsync(organizationId, v.Id, [v], ct)).GetValueOrDefault(v.Id) ?? [];
            var alerts = VehicleAlerts.For(v, pm, blocked, rules, parts, today);
            var driver = (await FleetQueries.CurrentDriversAsync(_db, organizationId, [v.Id], ct)).GetValueOrDefault(v.Id);

            var expenses = await _db.Expenses.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var fuel = await _db.FuelTransactions.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
            var maintenance = await _db.MaintenanceRecords.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active && x.CompletedAtUtc != null).SumAsync(x => x.ActualCost, ct) ?? 0;

            var counts = new VehicleTabCountsDto(
                await _db.VehicleParts.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active, ct),
                await _db.VehicleIssues.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active && x.Status != VehicleConstants.IssueResolved, ct),
                await _db.FuelTransactions.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active, ct),
                await _db.OdometerReadings.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active, ct),
                await _db.MaintenanceRecords.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active, ct),
                await _db.Expenses.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active, ct),
                await _db.Inspections.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active, ct),
                await _db.VehicleAssignments.CountAsync(x => x.OrganizationId == organizationId && x.VehicleId == v.Id && x.CurrentState == Active, ct));

            return ServiceResult<VehicleDetailDto>.Ok(new VehicleDetailDto(
                v.Id, Name(v),
                v.Make, v.Model, v.ModelYear, v.Category, v.OwnerType, v.OwnerName, v.Vin, v.PlateNumber, v.Status,
                v.CurrentOdometer, v.ReadingUnit, v.EngineType, v.FuelType, v.TankCapacity, TankUnit(v),
                v.PmTrigger, v.PmIntervalKm, v.PmIntervalDays, v.PmIntervalHours, v.PmLastServiceReading, v.PmLastServiceDate,
                v.PurchaseDate, v.PurchasePrice, v.Supplier, v.WarrantyUntil, v.ExpectedResidualValue,
                v.InsuranceProvider, v.InsurancePolicyNumber, v.InsuranceRenewalDate, v.InsuranceAnnualPremium,
                v.RegistrationAuthority, v.RegistrationNumber, v.RegistrationRenewalDate,
                v.Notes, v.PhotoKey is not null,
                pm, blocked, pm.Status == "overdue" ? rules.EscalatedTo(pm.DaysOverdue ?? 0) : null,
                DaysLeft(v.InsuranceRenewalDate), DaysLeft(v.RegistrationRenewalDate), DaysLeft(v.WarrantyUntil),
                expenses + fuel + maintenance,
                ToDriver(driver), alerts, counts));
        }

        // =====================================================================
        // Create / update / delete
        // =====================================================================
        public async Task<ServiceResult<VehicleDetailDto>> CreateAsync(Guid organizationId, Guid userId, string userName, VehicleUpsertRequest request, CancellationToken ct = default)
        {
            var errors = Validate(request, isCreate: true);
            if (errors.Count > 0) return ServiceResult<VehicleDetailDto>.Invalid(errors);
            var conflict = await CheckUniquenessAsync(organizationId, null, request, ct);
            if (conflict is not null) return conflict;
            var limit = await Company.PlanLimits.CheckAsync(_db, organizationId, vehicle: true, ct);
            if (limit is not null) return ServiceResult<VehicleDetailDto>.Conflict("PLAN_LIMIT", limit);

            var v = new Vehicle { OrganizationId = organizationId };
            Apply(v, request);
            v.CurrentOdometer = request.CurrentOdometer!.Value;
            v.Status = string.IsNullOrWhiteSpace(request.Status) ? VehicleConstants.StatusActive : request.Status!;

            await _uow.Repository<Vehicle>().AddAsyncGetID(v, userId, ct);
            await _uow.Repository<OdometerReading>().AddAsyncGetID(new OdometerReading
            {
                OrganizationId = organizationId,
                VehicleId = v.Id,
                ReadingDate = Today,
                Value = v.CurrentOdometer.Value,
                Source = VehicleConstants.ReadingInitial,
                RecordedBy = NullIfBlank(userName)
            }, userId, ct);
            return await GetAsync(organizationId, v.Id, ct);
        }

        public async Task<ServiceResult<VehicleDetailDto>> UpdateAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, VehicleUpsertRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<Vehicle>();
            var v = await repo.GetByIdAsync(vehicleId, ct);
            if (v is null || v.OrganizationId != organizationId || v.CurrentState != Active)
                return ServiceResult<VehicleDetailDto>.NotFound("Vehicle not found.");

            var errors = Validate(request, isCreate: false);
            if (errors.Count > 0) return ServiceResult<VehicleDetailDto>.Invalid(errors);
            var conflict = await CheckUniquenessAsync(organizationId, vehicleId, request, ct);
            if (conflict is not null) return conflict;

            // Odometer changes are readings. The value may not go below the highest recorded reading, except
            // when the only reading is the registration one (correcting a typo at registration).
            OdometerReading? readingToAdd = null;
            var newReading = request.CurrentOdometer;
            if (newReading is not null && newReading != v.CurrentOdometer)
            {
                var readings = await _db.OdometerReadings.AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active)
                    .ToListAsync(ct);
                var onlyInitial = readings.Count == 1 && readings[0].Source == VehicleConstants.ReadingInitial;
                if (onlyInitial)
                {
                    var initial = await _uow.Repository<OdometerReading>().GetByIdAsync(readings[0].Id, ct);
                    initial!.Value = newReading.Value;
                    await _uow.Repository<OdometerReading>().UpdateAsync(initial, userId, ct);
                }
                else
                {
                    var max = readings.Count == 0 ? 0 : readings.Max(x => x.Value);
                    if (newReading < max)
                        return ServiceResult<VehicleDetailDto>.Invalid(new Dictionary<string, string>
                        {
                            ["currentOdometer"] = $"Odometer can't go below the latest reading ({max:N0} {v.ReadingUnit}). Correct readings in Kilometer records."
                        });
                    readingToAdd = new OdometerReading
                    {
                        OrganizationId = organizationId, VehicleId = vehicleId, ReadingDate = Today,
                        Value = newReading.Value, Source = VehicleConstants.ReadingEdited, RecordedBy = NullIfBlank(userName)
                    };
                }
                v.CurrentOdometer = newReading;
            }
            if (request.PmLastServiceReading is not null && request.PmLastServiceReading > (v.CurrentOdometer ?? 0))
                return ServiceResult<VehicleDetailDto>.Invalid(new Dictionary<string, string>
                {
                    ["pmLastServiceReading"] = "Reading at last service can't be above the current odometer."
                });

            Apply(v, request);
            if (!string.IsNullOrWhiteSpace(request.Status)) v.Status = request.Status!;
            await repo.UpdateAsync(v, userId, ct);
            if (readingToAdd is not null) await _uow.Repository<OdometerReading>().AddAsyncGetID(readingToAdd, userId, ct);
            return await GetAsync(organizationId, vehicleId, ct);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            var v = await ActiveVehicles(organizationId).FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
            if (v is null) return ServiceResult<bool>.NotFound("Vehicle not found.");

            // Parts, readings, issues, fuel, maintenance, inspections and assignments go with the vehicle
            // (database cascade); expenses stay in the books as fleet-wide (FK set null).
            var deleted = await _uow.Repository<Vehicle>().DeleteAsync(vehicleId, ct);
            if (!deleted) return ServiceResult<bool>.NotFound("Vehicle not found.");
            if (v.PhotoKey is not null) await _storage.DeleteAsync(v.PhotoKey, ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<VehicleDetailDto>> SetStatusAsync(Guid organizationId, Guid userId, Guid vehicleId, VehicleStatusRequest request, CancellationToken ct = default)
        {
            if (!VehicleConstants.Statuses.Contains(request.Status))
                return ServiceResult<VehicleDetailDto>.Invalid(new Dictionary<string, string> { ["status"] = $"Status must be one of: {string.Join(", ", VehicleConstants.Statuses)}." });
            var repo = _uow.Repository<Vehicle>();
            var v = await repo.GetByIdAsync(vehicleId, ct);
            if (v is null || v.OrganizationId != organizationId || v.CurrentState != Active)
                return ServiceResult<VehicleDetailDto>.NotFound("Vehicle not found.");
            v.Status = request.Status;
            await repo.UpdateAsync(v, userId, ct);
            return await GetAsync(organizationId, vehicleId, ct);
        }

        // =====================================================================
        // Hand-off
        // =====================================================================
        public async Task<ServiceResult<VehicleHandOffResult>> HandOffAsync(Guid organizationId, Guid userId, string userName, Guid vehicleId, VehicleHandOffRequest request, CancellationToken ct = default)
        {
            var v = await ActiveVehicles(organizationId).FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
            if (v is null) return ServiceResult<VehicleHandOffResult>.NotFound("Vehicle not found.");

            var errors = new Dictionary<string, string>();
            var effective = (request.EffectiveDate ?? Today).Date;
            var note = NullIfBlank(request.Note);
            if (note is { Length: > 500 }) errors["note"] = "Note must be 500 characters or fewer.";

            var current = (await FleetQueries.CurrentDriversAsync(_db, organizationId, [vehicleId], ct)).GetValueOrDefault(vehicleId);
            if (request.DriverId is null && current is null) errors["driverId"] = "Select the driver to assign.";
            if (request.DriverId is not null && request.DriverId == current?.DriverId) errors["driverId"] = "This driver already holds the vehicle.";
            if (current is not null && effective < current.Since.Date) errors["effectiveDate"] = $"Effective date can't be before the current assignment started ({current.Since:dd MMM yyyy}).";
            if (request.DriverId is not null && v.Status == VehicleConstants.StatusRetired) errors["driverId"] = "A retired vehicle can't be assigned.";
            if (errors.Count > 0) return ServiceResult<VehicleHandOffResult>.Invalid(errors);

            if (request.DriverId is not null)
            {
                var driverOk = await _db.Drivers.AsNoTracking().AnyAsync(d => d.Id == request.DriverId && d.OrganizationId == organizationId && d.CurrentState == Active, ct);
                if (!driverOk) return ServiceResult<VehicleHandOffResult>.Invalid(new Dictionary<string, string> { ["driverId"] = "Driver not found." });

                var busy = await _db.VehicleAssignments.AsNoTracking()
                    .Where(a => a.OrganizationId == organizationId && a.DriverId == request.DriverId && a.VehicleId != vehicleId
                                && a.CurrentState == Active && (a.EndDate == null || a.EndDate > effective)
                                && a.Vehicle.CurrentState == Active)
                    .Select(a => a.Vehicle.PlateNumber)
                    .FirstOrDefaultAsync(ct);
                if (busy is not null)
                    return ServiceResult<VehicleHandOffResult>.Conflict("DRIVER_ALREADY_ASSIGNED",
                        $"This driver already holds vehicle {busy}. Hand that vehicle off first.", "driverId");
            }

            var repo = _uow.Repository<VehicleAssignment>();
            if (current is not null)
            {
                var open = await repo.GetByIdAsync(current.AssignmentId, ct);
                if (open is not null)
                {
                    open.EndDate = effective;
                    await repo.UpdateAsync(open, userId, ct);
                }
            }
            if (request.DriverId is not null)
            {
                await repo.AddAsyncGetID(new VehicleAssignment
                {
                    OrganizationId = organizationId,
                    VehicleId = vehicleId,
                    DriverId = request.DriverId.Value,
                    StartDate = effective,
                    AssignmentType = "Primary",
                    Notes = note
                }, userId, ct);
            }
            // Optional hand-off inspection with the built-in checklist; the hand-off itself is already saved.
            Guid? inspectionId = null;
            string? inspectionMessage = null;
            if (request.PerformInspection)
            {
                var templateId = await _templates.EnsureHandoffTemplateAsync(organizationId, userId, ct);
                if (templateId == Guid.Empty) inspectionMessage = "The parts catalogue is empty, so the hand-off checklist can't be built yet.";
                else
                {
                    var started = await _inspections.StartAsync(organizationId, userId, new Inspections.Dtos.InspectionStartRequest
                    {
                        VehicleId = vehicleId, TemplateId = templateId, Odometer = v.CurrentOdometer ?? 0,
                        InspectorName = string.IsNullOrWhiteSpace(request.InspectorName) ? userName : request.InspectorName
                    }, ct);
                    if (started.Succeeded) inspectionId = started.Value!.Id;
                    else if (started.Error!.Code == "INSPECTION_IN_PROGRESS")
                    {
                        inspectionId = await _db.Inspections.AsNoTracking()
                            .Where(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.CurrentState == Active && x.Status == InspectionConstants.StatusInProgress)
                            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
                        inspectionMessage = "An inspection was already in progress on this vehicle — it was reopened.";
                    }
                    else inspectionMessage = started.Error.Message;
                }
            }
            var detail = await GetAsync(organizationId, vehicleId, ct);
            return ServiceResult<VehicleHandOffResult>.Ok(new VehicleHandOffResult(detail.Value!, inspectionId, inspectionMessage));
        }

        // =====================================================================
        // Photo
        // =====================================================================
        public async Task<ServiceResult<bool>> SetPhotoAsync(Guid organizationId, Guid userId, Guid vehicleId, FileUpload file, CancellationToken ct = default)
        {
            var repo = _uow.Repository<Vehicle>();
            var v = await repo.GetByIdAsync(vehicleId, ct);
            if (v is null || v.OrganizationId != organizationId || v.CurrentState != Active)
                return ServiceResult<bool>.NotFound("Vehicle not found.");

            var ext = Path.GetExtension(file.FileName);
            string? error = null;
            if (file.Length <= 0) error = "The uploaded file is empty.";
            else if (file.Length > MaxPhotoBytes) error = "The photo must be 5 MB or smaller.";
            else if (!PhotoTypes.ContainsKey(ext) || !await IsImageAsync(file.Content, ext)) error = "Only JPG, PNG or WEBP images are allowed.";
            if (error is not null) return ServiceResult<bool>.Invalid(new Dictionary<string, string> { ["photo"] = error });

            var oldKey = v.PhotoKey;
            v.PhotoKey = await _storage.SaveAsync(organizationId, "vehicle-photos", file.Content, ext.ToLowerInvariant(), ct);
            v.PhotoContentType = PhotoTypes[ext];
            try
            {
                await repo.UpdateAsync(v, userId, ct);
            }
            catch
            {
                await _storage.DeleteAsync(v.PhotoKey, ct);
                throw;
            }
            if (oldKey is not null) await _storage.DeleteAsync(oldKey, ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> DeletePhotoAsync(Guid organizationId, Guid userId, Guid vehicleId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<Vehicle>();
            var v = await repo.GetByIdAsync(vehicleId, ct);
            if (v is null || v.OrganizationId != organizationId || v.CurrentState != Active)
                return ServiceResult<bool>.NotFound("Vehicle not found.");
            if (v.PhotoKey is null) return ServiceResult<bool>.Ok(true);
            var key = v.PhotoKey;
            v.PhotoKey = null;
            v.PhotoContentType = null;
            await repo.UpdateAsync(v, userId, ct);
            await _storage.DeleteAsync(key, ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<FileDownload>> OpenPhotoAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
        {
            var v = await ActiveVehicles(organizationId).FirstOrDefaultAsync(x => x.Id == vehicleId, ct);
            if (v?.PhotoKey is null) return ServiceResult<FileDownload>.NotFound("Photo not found.");
            var stream = await _storage.OpenReadAsync(v.PhotoKey, ct);
            if (stream is null) return ServiceResult<FileDownload>.NotFound("Photo not found.");
            return ServiceResult<FileDownload>.Ok(new FileDownload(stream, $"{v.PlateNumber}{Path.GetExtension(v.PhotoKey)}", v.PhotoContentType ?? "image/jpeg"));
        }

        // =====================================================================
        // Fleet alerts (notifications)
        // =====================================================================
        public async Task<List<(Guid VehicleId, string VehicleName, string Plate, VehicleAlertDto Alert)>> FleetAlertsAsync(Guid organizationId, CancellationToken ct = default)
        {
            var vehicles = await ActiveVehicles(organizationId).ToListAsync(ct);
            var rules = await PmRules.LoadAsync(_db, organizationId, ct);
            var parts = await PartWearByVehicleAsync(organizationId, null, vehicles, ct);
            var today = Today;
            var result = new List<(Guid, string, string, VehicleAlertDto)>();
            foreach (var v in vehicles)
            {
                var pm = PmCalculator.Evaluate(v, today, rules);
                foreach (var a in VehicleAlerts.For(v, pm, PmCalculator.DispatchBlocked(v, pm, rules), rules, parts.GetValueOrDefault(v.Id) ?? [], today))
                    result.Add((v.Id, Name(v), v.PlateNumber, a));
            }
            return result;
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private IQueryable<Vehicle> ActiveVehicles(Guid organizationId) =>
            _db.Vehicles.AsNoTracking().Where(v => v.OrganizationId == organizationId && v.CurrentState == (int)CurrentStatusType.Active);

        /// <summary>Wear of the fitted (non-retired) parts, per vehicle.</summary>
        private async Task<Dictionary<Guid, List<PartWearInfo>>> PartWearByVehicleAsync(Guid organizationId, Guid? vehicleId, IReadOnlyList<Vehicle> vehicles, CancellationToken ct)
        {
            var byId = vehicles.ToDictionary(v => v.Id);
            var rows = await _db.VehicleParts.AsNoTracking()
                .Where(p => p.OrganizationId == organizationId && p.CurrentState == Active && p.Status != VehicleConstants.PartRetired
                            && (vehicleId == null || p.VehicleId == vehicleId))
                .Select(p => new { Part = p, p.PresetPart.Name })
                .ToListAsync(ct);
            var today = Today;
            return rows
                .Where(r => byId.ContainsKey(r.Part.VehicleId))
                .GroupBy(r => r.Part.VehicleId)
                .ToDictionary(g => g.Key, g => g.Select(r =>
                {
                    var v = byId[r.Part.VehicleId];
                    return new PartWearInfo(r.Part.Id, PartCode(r.Part.Number), r.Name, PartWearCalculator.Evaluate(r.Part, v.CurrentOdometer, v.ReadingUnit, today));
                }).ToList());
        }

        private async Task<ServiceResult<VehicleDetailDto>?> CheckUniquenessAsync(Guid organizationId, Guid? vehicleId, VehicleUpsertRequest r, CancellationToken ct)
        {
            var plate = NormalizePlate(r.PlateNumber);
            if (await _db.Vehicles.AsNoTracking().AnyAsync(v => v.OrganizationId == organizationId && v.PlateNumber == plate && v.Id != vehicleId, ct))
                return ServiceResult<VehicleDetailDto>.Conflict("PLATE_EXISTS", $"Plate {plate} is already registered in this organization.", "plateNumber");
            var vin = r.Vin?.Trim().ToUpperInvariant();
            if (vin is not null && await _db.Vehicles.AsNoTracking().AnyAsync(v => v.OrganizationId == organizationId && v.Vin == vin && v.Id != vehicleId, ct))
                return ServiceResult<VehicleDetailDto>.Conflict("VIN_EXISTS", "This VIN is already registered in this organization.", "vin");
            return null;
        }

        private static Dictionary<string, string> Validate(VehicleUpsertRequest r, bool isCreate)
        {
            var e = new Dictionary<string, string>();
            var year = DateTime.UtcNow.Year;

            Required(e, "make", r.Make, "Make", 60);
            Required(e, "model", r.Model, "Model", 60);
            if (r.ModelYear is null) e["modelYear"] = "Year is required.";
            else if (r.ModelYear < 1980 || r.ModelYear > year + 1) e["modelYear"] = $"Year must be between 1980 and {year + 1}.";
            OneOf(e, "category", r.Category, VehicleConstants.Categories, "Category");
            OneOf(e, "ownerType", r.OwnerType, VehicleConstants.OwnerTypes, "Owner type");
            if (r.OwnerType is VehicleConstants.OwnerClient or VehicleConstants.OwnerProvider && string.IsNullOrWhiteSpace(r.OwnerName))
                e["ownerName"] = "Owner name is required for client or provider vehicles.";
            else if (r.OwnerName is { Length: > 120 }) e["ownerName"] = "Owner name must be 120 characters or fewer.";

            var vin = r.Vin?.Trim().ToUpperInvariant() ?? "";
            if (vin.Length == 0) e["vin"] = "VIN is required.";
            else if (!VinPattern.IsMatch(vin)) e["vin"] = "VIN must be 17 letters and digits (I, O and Q are not used).";

            var plate = NormalizePlate(r.PlateNumber);
            if (plate.Length == 0) e["plateNumber"] = "Plate number is required.";
            else if (plate.Length > 20) e["plateNumber"] = "Plate number must be 20 characters or fewer.";
            if (!string.IsNullOrWhiteSpace(r.Status)) OneOf(e, "status", r.Status, VehicleConstants.Statuses, "Status");

            // Usage & power
            if (isCreate && r.CurrentOdometer is null) e["currentOdometer"] = "Odometer is required.";
            if (r.CurrentOdometer is < 0) e["currentOdometer"] = "Odometer can't be negative.";
            var unit = string.IsNullOrWhiteSpace(r.ReadingUnit) ? VehicleConstants.UnitKm : r.ReadingUnit!;
            OneOf(e, "readingUnit", unit, VehicleConstants.ReadingUnits, "Reading unit");
            OneOf(e, "engineType", r.EngineType, VehicleConstants.EngineTypes, "Engine type");
            OneOf(e, "fuelType", r.FuelType, VehicleConstants.FuelTypes, "Fuel type");
            if (r.EngineType == VehicleConstants.EngineElectric && !string.IsNullOrWhiteSpace(r.FuelType) && r.FuelType != VehicleConstants.FuelElectric)
                e["fuelType"] = "An electric engine uses the Electric fuel type.";
            if (r.TankCapacity is <= 0) e["tankCapacity"] = "Tank capacity must be greater than 0.";

            // PM criteria: the trigger decides which intervals are needed.
            var trigger = string.IsNullOrWhiteSpace(r.PmTrigger) ? VehicleConstants.PmUsage : r.PmTrigger!;
            OneOf(e, "pmTrigger", trigger, VehicleConstants.PmTriggers, "Trigger type");
            if (r.PmIntervalKm is <= 0) e["pmIntervalKm"] = "Distance interval must be greater than 0.";
            if (r.PmIntervalDays is <= 0) e["pmIntervalDays"] = "Time interval must be greater than 0.";
            if (r.PmIntervalHours is <= 0) e["pmIntervalHours"] = "Engine hour interval must be greater than 0.";
            var hoursUnit = unit == VehicleConstants.UnitHours;
            var needsUsage = trigger is VehicleConstants.PmUsage or VehicleConstants.PmHybrid;
            if (needsUsage && !hoursUnit && r.PmIntervalKm is null) e["pmIntervalKm"] = "Set the distance interval for a usage-based trigger.";
            if (needsUsage && hoursUnit && r.PmIntervalHours is null) e["pmIntervalHours"] = "Set the engine hour interval (the reading unit is engine hours).";
            if (trigger is VehicleConstants.PmTime or VehicleConstants.PmHybrid && r.PmIntervalDays is null) e["pmIntervalDays"] = "Set the time interval for a time-based trigger.";
            if (trigger == VehicleConstants.PmHours)
            {
                if (!hoursUnit) e["readingUnit"] = "An engine-hour trigger needs the reading unit set to engine hours.";
                if (r.PmIntervalHours is null) e["pmIntervalHours"] = "Set the engine hour interval.";
            }
            if (r.PmLastServiceReading is < 0) e["pmLastServiceReading"] = "Reading can't be negative.";
            else if (isCreate && r.PmLastServiceReading is not null && r.CurrentOdometer is not null && r.PmLastServiceReading > r.CurrentOdometer)
                e["pmLastServiceReading"] = "Reading at last service can't be above the current odometer.";
            if (r.PmLastServiceDate?.Date > Today) e["pmLastServiceDate"] = "Last service date can't be in the future.";

            // Purchase
            if (r.PurchaseDate?.Date > Today) e["purchaseDate"] = "Purchase date can't be in the future.";
            if (r.PurchasePrice is < 0) e["purchasePrice"] = "Purchase cost can't be negative.";
            if (r.ExpectedResidualValue is < 0) e["expectedResidualValue"] = "Residual value can't be negative.";
            else if (r.ExpectedResidualValue is not null && r.PurchasePrice is not null && r.ExpectedResidualValue > r.PurchasePrice)
                e["expectedResidualValue"] = "Residual value can't exceed the purchase cost.";
            if (r.WarrantyUntil is not null && r.PurchaseDate is not null && r.WarrantyUntil < r.PurchaseDate) e["warrantyUntil"] = "Warranty can't end before the purchase date.";
            Optional(e, "supplier", r.Supplier, "Supplier", 120);

            // Insurance & registration
            Optional(e, "insuranceProvider", r.InsuranceProvider, "Provider", 120);
            Optional(e, "insurancePolicyNumber", r.InsurancePolicyNumber, "Policy number", 60);
            if (r.InsuranceAnnualPremium is < 0) e["insuranceAnnualPremium"] = "Premium can't be negative.";
            Optional(e, "registrationAuthority", r.RegistrationAuthority, "Issuing authority", 120);
            Optional(e, "registrationNumber", r.RegistrationNumber, "Registration number", 60);
            Optional(e, "notes", r.Notes, "Notes", 1000);
            return e;
        }

        private static void Apply(Vehicle v, VehicleUpsertRequest r)
        {
            v.Make = r.Make.Trim();
            v.Model = r.Model.Trim();
            v.ModelYear = r.ModelYear!.Value;
            v.Category = r.Category;
            v.OwnerType = r.OwnerType;
            v.OwnerName = NullIfBlank(r.OwnerName);
            v.Vin = r.Vin!.Trim().ToUpperInvariant();
            v.PlateNumber = NormalizePlate(r.PlateNumber);
            v.ReadingUnit = string.IsNullOrWhiteSpace(r.ReadingUnit) ? VehicleConstants.UnitKm : r.ReadingUnit!;
            v.EngineType = r.EngineType;
            v.FuelType = r.FuelType;
            v.TankCapacity = r.TankCapacity;
            v.PmTrigger = string.IsNullOrWhiteSpace(r.PmTrigger) ? VehicleConstants.PmUsage : r.PmTrigger!;
            v.PmIntervalKm = r.PmIntervalKm;
            v.PmIntervalDays = r.PmIntervalDays;
            v.PmIntervalHours = r.PmIntervalHours;
            v.PmLastServiceReading = r.PmLastServiceReading ?? v.PmLastServiceReading ?? r.CurrentOdometer;
            v.PmLastServiceDate = r.PmLastServiceDate?.Date ?? v.PmLastServiceDate ?? Today;
            v.PurchaseDate = r.PurchaseDate?.Date;
            v.PurchasePrice = r.PurchasePrice;
            v.Supplier = NullIfBlank(r.Supplier);
            v.WarrantyUntil = r.WarrantyUntil?.Date;
            v.ExpectedResidualValue = r.ExpectedResidualValue;
            v.InsuranceProvider = NullIfBlank(r.InsuranceProvider);
            v.InsurancePolicyNumber = NullIfBlank(r.InsurancePolicyNumber);
            v.InsuranceRenewalDate = r.InsuranceRenewalDate?.Date;
            v.InsuranceAnnualPremium = r.InsuranceAnnualPremium;
            v.RegistrationAuthority = NullIfBlank(r.RegistrationAuthority);
            v.RegistrationNumber = NullIfBlank(r.RegistrationNumber);
            v.RegistrationRenewalDate = r.RegistrationRenewalDate?.Date;
            v.Notes = NullIfBlank(r.Notes);
        }

        internal static string Name(Vehicle v) => $"{v.Make} {v.Model}".Trim();
        internal static string PartCode(int number) => $"PRT-{number:0000}";
        internal static string TankUnit(Vehicle v) => v.FuelType == VehicleConstants.FuelElectric ? "kWh" : "L";

        private static VehicleDriverDto? ToDriver(CurrentDriver? d) =>
            d is null ? null : new VehicleDriverDto(d.DriverId, d.FullName, d.LicenseClass, d.LicenseNumber, d.Rating, d.Phone, d.Since == default ? null : d.Since);

        private static string NormalizePlate(string? plate) => Regex.Replace(plate?.Trim().ToUpperInvariant() ?? "", @"\s+", " ");
        private static int? DaysLeft(DateTime? date) => date is null ? null : (date.Value.Date - Today).Days;
        private static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        private static bool Contains(string? value, string term) => value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

        private static void Required(Dictionary<string, string> e, string key, string? value, string label, int max)
        {
            var v = value?.Trim() ?? "";
            if (v.Length == 0) e[key] = $"{label} is required.";
            else if (v.Length > max) e[key] = $"{label} must be {max} characters or fewer.";
        }

        private static void Optional(Dictionary<string, string> e, string key, string? value, string label, int max)
        {
            if (value is not null && value.Trim().Length > max) e[key] = $"{label} must be {max} characters or fewer.";
        }

        private static void OneOf(Dictionary<string, string> e, string key, string? value, string[] allowed, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) e[key] = $"{label} is required.";
            else if (!allowed.Contains(value)) e[key] = $"{label} must be one of: {string.Join(", ", allowed)}.";
        }

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
