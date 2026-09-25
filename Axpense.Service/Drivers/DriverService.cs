using System.Net.Mail;
using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Data.Entities.CostEntities;
using Axpense.Data.Entities.DriverEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Service.Common;
using Axpense.Service.Drivers.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Drivers
{
    /// <summary>
    /// Business logic for the Drivers module: validation, uniqueness, licence / document expiry
    /// rules, current-vehicle resolution and the performance history.
    /// Every query is scoped to the caller's organization (tenant isolation).
    /// </summary>
    public sealed class DriverService : IDriverService
    {
        private const long MaxDocumentBytes = 5 * 1024 * 1024;
        private static readonly Dictionary<string, string> AllowedDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".pdf"] = "application/pdf"
        };

        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;
        private readonly IFileStorage _storage;

        public DriverService(AxpenseDbContext db, IUnitOfWork uow, IFileStorage storage)
        {
            _db = db;
            _uow = uow;
            _storage = storage;
        }

        private static DateTime Today => DateTime.UtcNow.Date;

        // =====================================================================
        // Queries
        // =====================================================================
        public async Task<DriverListResponse> ListAsync(Guid organizationId, DriverListQuery query, CancellationToken ct = default)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);

            var baseQuery = ActiveDrivers(organizationId);
            var total = await baseQuery.CountAsync(ct);

            var filtered = baseQuery;
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = query.Q.Trim().ToLower();
                filtered = filtered.Where(d =>
                    d.FullName.ToLower().Contains(term) ||
                    d.EmployeeNumber.ToLower().Contains(term) ||
                    (d.LicenseNumber != null && d.LicenseNumber.ToLower().Contains(term)));
            }
            if (!string.IsNullOrWhiteSpace(query.Status))
                filtered = filtered.Where(d => d.Status == query.Status);

            var filteredCount = await filtered.CountAsync(ct);

            var reminderLimit = Today.AddDays(DriverConstants.ExpiryReminderDays);
            var rows = await filtered
                .OrderBy(d => d.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id, d.FullName, d.EmployeeNumber, d.LicenseClass, d.LicenseNumber,
                    d.LicenseExpiryDate, d.Rating, d.Status,
                    DocumentCount = d.Documents.Count(x => x.CurrentState == (int)CurrentStatusType.Active),
                    ExpiringDocumentCount = d.Documents.Count(x => x.CurrentState == (int)CurrentStatusType.Active && x.ExpiryDate <= reminderLimit)
                })
                .ToListAsync(ct);

            var vehicles = await CurrentVehiclesAsync(organizationId, rows.Select(r => r.Id).ToList(), ct);

            var items = rows.Select(r => new DriverListItemDto(
                r.Id, r.FullName, r.EmployeeNumber, r.LicenseClass, r.LicenseNumber,
                r.LicenseExpiryDate, DaysLeft(r.LicenseExpiryDate),
                vehicles.GetValueOrDefault(r.Id),
                r.DocumentCount, r.ExpiringDocumentCount, r.Rating, r.Status)).ToList();

            return new DriverListResponse(items, filteredCount, total, page, pageSize);
        }

        public async Task<string> NextEmployeeNumberAsync(Guid organizationId, CancellationToken ct = default)
        {
            var numbers = await _db.Drivers.AsNoTracking()
                .Where(d => d.OrganizationId == organizationId && d.EmployeeNumber.StartsWith(DriverConstants.EmployeeNumberPrefix))
                .Select(d => d.EmployeeNumber)
                .ToListAsync(ct);

            var max = numbers
                .Select(n => int.TryParse(n[DriverConstants.EmployeeNumberPrefix.Length..], out var v) ? v : 0)
                .DefaultIfEmpty(DriverConstants.EmployeeNumberSeed)
                .Max();

            return $"{DriverConstants.EmployeeNumberPrefix}{Math.Max(max, DriverConstants.EmployeeNumberSeed) + 1}";
        }

        public async Task<ServiceResult<DriverProfileDto>> GetProfileAsync(Guid organizationId, Guid driverId, CancellationToken ct = default)
        {
            var driver = await ActiveDrivers(organizationId).FirstOrDefaultAsync(d => d.Id == driverId, ct);
            if (driver is null) return ServiceResult<DriverProfileDto>.NotFound("Driver not found.");

            var documents = await _db.DriverDocuments.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.DriverId == driverId && x.CurrentState == (int)CurrentStatusType.Active)
                .OrderBy(x => x.ExpiryDate)
                .ToListAsync(ct);

            var assignments = await _db.VehicleAssignments.AsNoTracking()
                .Where(a => a.OrganizationId == organizationId && a.DriverId == driverId && a.CurrentState == (int)CurrentStatusType.Active)
                .Select(a => new
                {
                    a.VehicleId, a.StartDate, a.EndDate, a.Notes,
                    VehicleName = a.Vehicle.Make + " " + a.Vehicle.Model,
                    a.Vehicle.PlateNumber
                })
                .ToListAsync(ct);

            var current = assignments
                .Where(a => a.StartDate.Date <= Today && (a.EndDate == null || a.EndDate.Value.Date > Today))
                .OrderByDescending(a => a.StartDate)
                .FirstOrDefault();

            // ---- Performance history: assignments + fuel bought while the driver held the vehicle ----
            var timeline = assignments.Select(a => new DriverTimelineEventDto(
                a.StartDate, "assignment", current != null && a.VehicleId == current.VehicleId && a.StartDate == current.StartDate,
                a.VehicleName, a.Notes, a.EndDate, null, null, null)).ToList();

            if (assignments.Count > 0)
            {
                var vehicleIds = assignments.Select(a => a.VehicleId).Distinct().ToList();
                var from = assignments.Min(a => a.StartDate);
                var fuel = await _db.FuelTransactions.AsNoTracking()
                    .Where(f => f.OrganizationId == organizationId && vehicleIds.Contains(f.VehicleId) && f.TransactionDate >= from
                                && f.CurrentState == (int)CurrentStatusType.Active)
                    .OrderByDescending(f => f.TransactionDate)
                    .Take(200)
                    .ToListAsync(ct);

                timeline.AddRange(fuel
                    .Where(f => assignments.Any(a => a.VehicleId == f.VehicleId && f.TransactionDate >= a.StartDate
                                                     && (a.EndDate == null || f.TransactionDate <= a.EndDate)))
                    .Select(f => new DriverTimelineEventDto(
                        f.TransactionDate, "fuel", false,
                        assignments.First(a => a.VehicleId == f.VehicleId).VehicleName,
                        null, null, f.Station, f.QuantityLiters, f.TotalAmount)));
            }

            // Inspections and issues are not yet linked to drivers (no inspector → driver link and no
            // Issues module). They report 0 until those modules land.
            var stats = new DriverStatsDto(driver.Rating, 0, 0, 0, 0);

            return ServiceResult<DriverProfileDto>.Ok(new DriverProfileDto(
                driver.Id, driver.FullName, driver.EmployeeNumber, driver.Status, driver.Phone, driver.Email,
                driver.NationalId, driver.HireDate, driver.LicenseNumber, driver.LicenseClass,
                driver.LicenseIssuedDate, driver.LicenseExpiryDate, DaysLeft(driver.LicenseExpiryDate),
                driver.Rating, driver.Notes,
                current is null ? null : new VehicleRefDto(current.VehicleId, current.VehicleName, current.PlateNumber),
                stats,
                documents.Select(ToDto).ToList(),
                timeline.OrderByDescending(e => e.Date).Take(10).ToList()));
        }

        // =====================================================================
        // Commands
        // =====================================================================
        public async Task<ServiceResult<DriverProfileDto>> CreateAsync(Guid organizationId, Guid userId, DriverUpsertRequest request, CancellationToken ct = default)
        {
            var errors = Validate(request);
            if (errors.Count > 0) return ServiceResult<DriverProfileDto>.Invalid(errors);

            var employeeNumber = string.IsNullOrWhiteSpace(request.EmployeeNumber)
                ? await NextEmployeeNumberAsync(organizationId, ct)
                : request.EmployeeNumber.Trim();

            var conflict = await CheckUniquenessAsync(organizationId, null, employeeNumber, request.LicenseNumber.Trim(), ct);
            if (conflict is not null) return conflict;

            var driver = new Driver { OrganizationId = organizationId, EmployeeNumber = employeeNumber };
            Apply(driver, request);

            var (success, id) = await _uow.Repository<Driver>().AddAsyncGetID(driver, userId, ct);
            if (!success) return ServiceResult<DriverProfileDto>.Conflict("DRIVER_NOT_CREATED", "Could not create the driver.");
            return await GetProfileAsync(organizationId, id, ct);
        }

        public async Task<ServiceResult<DriverProfileDto>> UpdateAsync(Guid organizationId, Guid userId, Guid driverId, DriverUpsertRequest request, CancellationToken ct = default)
        {
            var errors = Validate(request);
            if (errors.Count > 0) return ServiceResult<DriverProfileDto>.Invalid(errors);

            var repo = _uow.Repository<Driver>();
            var driver = await repo.GetByIdAsync(driverId, ct);
            if (driver is null || driver.OrganizationId != organizationId || driver.CurrentState != (int)CurrentStatusType.Active)
                return ServiceResult<DriverProfileDto>.NotFound("Driver not found.");

            var employeeNumber = string.IsNullOrWhiteSpace(request.EmployeeNumber) ? driver.EmployeeNumber : request.EmployeeNumber.Trim();
            var conflict = await CheckUniquenessAsync(organizationId, driverId, employeeNumber, request.LicenseNumber.Trim(), ct);
            if (conflict is not null) return conflict;

            driver.EmployeeNumber = employeeNumber;
            Apply(driver, request);
            await repo.UpdateAsync(driver, userId, ct);
            return await GetProfileAsync(organizationId, driverId, ct);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid driverId, CancellationToken ct = default)
        {
            var exists = await ActiveDrivers(organizationId).AnyAsync(d => d.Id == driverId, ct);
            if (!exists) return ServiceResult<bool>.NotFound("Driver not found.");

            // Assignments and documents are removed by the database cascade; collect the scans first.
            var fileKeys = await _db.DriverDocuments.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.DriverId == driverId && x.FileKey != null)
                .Select(x => x.FileKey!)
                .ToListAsync(ct);

            var deleted = await _uow.Repository<Driver>().DeleteAsync(driverId, ct);
            if (!deleted) return ServiceResult<bool>.NotFound("Driver not found.");

            foreach (var key in fileKeys) await _storage.DeleteAsync(key, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================================
        // Documents
        // =====================================================================
        public async Task<ServiceResult<DriverDocumentDto>> AddDocumentAsync(Guid organizationId, Guid userId, Guid driverId, DriverDocumentCreateRequest request, FileUpload? file, CancellationToken ct = default)
        {
            if (!await ActiveDrivers(organizationId).AnyAsync(d => d.Id == driverId, ct))
                return ServiceResult<DriverDocumentDto>.NotFound("Driver not found.");

            var errors = new Dictionary<string, string>();
            var name = request.Name?.Trim() ?? "";
            if (name.Length == 0) errors["name"] = "Document name is required.";
            else if (name.Length > 150) errors["name"] = "Document name must be 150 characters or fewer.";
            if (request.ExpiryDate is null) errors["expiryDate"] = "Expiry date is required.";

            string? extension = null;
            if (file is not null)
            {
                extension = Path.GetExtension(file.FileName);
                if (file.Length <= 0) errors["file"] = "The uploaded file is empty.";
                else if (file.Length > MaxDocumentBytes) errors["file"] = "The file must be 5 MB or smaller.";
                else if (!AllowedDocumentTypes.ContainsKey(extension) || !await HasValidSignatureAsync(file.Content, extension))
                    errors["file"] = "Only JPG, PNG or PDF files are allowed.";
            }
            if (errors.Count > 0) return ServiceResult<DriverDocumentDto>.Invalid(errors);

            var document = new DriverDocument
            {
                OrganizationId = organizationId,
                DriverId = driverId,
                Name = name,
                ExpiryDate = request.ExpiryDate!.Value.Date
            };

            if (file is not null)
            {
                document.FileKey = await _storage.SaveAsync(organizationId, "driver-documents", file.Content, extension!, ct);
                document.FileName = Path.GetFileName(file.FileName);
                document.ContentType = AllowedDocumentTypes[extension!];
                document.FileSize = file.Length;
            }

            try
            {
                await _uow.Repository<DriverDocument>().AddAsyncGetID(document, userId, ct);
            }
            catch
            {
                if (document.FileKey is not null) await _storage.DeleteAsync(document.FileKey, ct);
                throw;
            }
            return ServiceResult<DriverDocumentDto>.Ok(ToDto(document));
        }

        public async Task<ServiceResult<bool>> DeleteDocumentAsync(Guid organizationId, Guid driverId, Guid documentId, CancellationToken ct = default)
        {
            var document = await FindDocumentAsync(organizationId, driverId, documentId, ct);
            if (document is null) return ServiceResult<bool>.NotFound("Document not found.");

            await _uow.Repository<DriverDocument>().DeleteAsync(documentId, ct);
            if (document.FileKey is not null) await _storage.DeleteAsync(document.FileKey, ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<FileDownload>> OpenDocumentFileAsync(Guid organizationId, Guid driverId, Guid documentId, CancellationToken ct = default)
        {
            var document = await FindDocumentAsync(organizationId, driverId, documentId, ct);
            if (document?.FileKey is null) return ServiceResult<FileDownload>.NotFound("No file is attached to this document.");

            var stream = await _storage.OpenReadAsync(document.FileKey, ct);
            if (stream is null) return ServiceResult<FileDownload>.NotFound("The attached file could not be found.");
            return ServiceResult<FileDownload>.Ok(new FileDownload(stream, document.FileName ?? "document", document.ContentType ?? "application/octet-stream"));
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private IQueryable<Driver> ActiveDrivers(Guid organizationId) =>
            _db.Drivers.AsNoTracking().Where(d => d.OrganizationId == organizationId && d.CurrentState == (int)CurrentStatusType.Active);

        private Task<DriverDocument?> FindDocumentAsync(Guid organizationId, Guid driverId, Guid documentId, CancellationToken ct) =>
            _db.DriverDocuments.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == documentId && x.DriverId == driverId && x.OrganizationId == organizationId &&
                x.CurrentState == (int)CurrentStatusType.Active, ct);

        /// <summary>Vehicle each driver holds today (open assignment covering today; latest start wins).</summary>
        private async Task<Dictionary<Guid, VehicleRefDto>> CurrentVehiclesAsync(Guid organizationId, List<Guid> driverIds, CancellationToken ct)
        {
            if (driverIds.Count == 0) return new();
            var today = Today;
            var open = await _db.VehicleAssignments.AsNoTracking()
                .Where(a => a.OrganizationId == organizationId && driverIds.Contains(a.DriverId)
                            && a.CurrentState == (int)CurrentStatusType.Active
                            && a.StartDate <= today.AddDays(1) && (a.EndDate == null || a.EndDate > today))
                .Select(a => new { a.DriverId, a.StartDate, a.VehicleId, Name = a.Vehicle.Make + " " + a.Vehicle.Model, a.Vehicle.PlateNumber })
                .ToListAsync(ct);

            return open
                .Where(a => a.StartDate.Date <= today)
                .GroupBy(a => a.DriverId)
                .ToDictionary(g => g.Key, g =>
                {
                    var a = g.OrderByDescending(x => x.StartDate).First();
                    return new VehicleRefDto(a.VehicleId, a.Name, a.PlateNumber);
                });
        }

        private async Task<ServiceResult<DriverProfileDto>?> CheckUniquenessAsync(Guid organizationId, Guid? driverId, string employeeNumber, string licenseNumber, CancellationToken ct)
        {
            var others = _db.Drivers.AsNoTracking().Where(d => d.OrganizationId == organizationId && (driverId == null || d.Id != driverId));

            if (await others.AnyAsync(d => d.EmployeeNumber == employeeNumber, ct))
                return ServiceResult<DriverProfileDto>.Conflict("EMPLOYEE_NUMBER_TAKEN", $"Employee number {employeeNumber} is already used by another driver.", "employeeNumber");

            if (await others.AnyAsync(d => d.LicenseNumber == licenseNumber, ct))
                return ServiceResult<DriverProfileDto>.Conflict("LICENSE_NUMBER_TAKEN", $"Licence number {licenseNumber} is already registered to another driver.", "licenseNumber");

            return null;
        }

        private static Dictionary<string, string> Validate(DriverUpsertRequest r)
        {
            var e = new Dictionary<string, string>();
            var name = r.FullName?.Trim() ?? "";
            if (name.Length == 0) e["fullName"] = "Full name is required.";
            else if (name.Length > 150) e["fullName"] = "Full name must be 150 characters or fewer.";

            if (!string.IsNullOrWhiteSpace(r.EmployeeNumber) && r.EmployeeNumber.Trim().Length > 30)
                e["employeeNumber"] = "Employee number must be 30 characters or fewer.";

            if (!DriverConstants.Statuses.Contains(r.Status))
                e["status"] = $"Status must be one of: {string.Join(", ", DriverConstants.Statuses)}.";

            if ((r.Phone?.Trim().Length ?? 0) > 30) e["phone"] = "Phone must be 30 characters or fewer.";
            if ((r.NationalId?.Trim().Length ?? 0) > 30) e["nationalId"] = "National ID must be 30 characters or fewer.";

            if (!string.IsNullOrWhiteSpace(r.Email))
            {
                var email = r.Email.Trim();
                if (email.Length > 200 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
                    e["email"] = "Enter a valid email address.";
            }

            var licence = r.LicenseNumber?.Trim() ?? "";
            if (licence.Length == 0) e["licenseNumber"] = "Licence number is required.";
            else if (licence.Length > 50) e["licenseNumber"] = "Licence number must be 50 characters or fewer.";

            if (!DriverConstants.LicenseClasses.Contains(r.LicenseClass))
                e["licenseClass"] = "Select a valid licence class.";

            if (r.LicenseExpiryDate is null) e["licenseExpiryDate"] = "Licence expiry date is required.";
            else if (r.LicenseIssuedDate is not null && r.LicenseExpiryDate.Value.Date <= r.LicenseIssuedDate.Value.Date)
                e["licenseExpiryDate"] = "Expiry date must be after the issue date.";

            if (r.Rating is not null && (r.Rating < DriverConstants.MinRating || r.Rating > DriverConstants.MaxRating))
                e["rating"] = "Rating must be between 0 and 5.";

            return e;
        }

        private static void Apply(Driver d, DriverUpsertRequest r)
        {
            d.FullName = r.FullName.Trim();
            d.Status = r.Status;
            d.Phone = r.Phone?.Trim() ?? "";
            d.Email = NullIfBlank(r.Email);
            d.NationalId = NullIfBlank(r.NationalId);
            d.HireDate = r.HireDate?.Date;
            d.LicenseNumber = r.LicenseNumber.Trim();
            d.LicenseClass = r.LicenseClass;
            d.LicenseIssuedDate = r.LicenseIssuedDate?.Date;
            d.LicenseExpiryDate = r.LicenseExpiryDate?.Date;
            d.Rating = Math.Round(r.Rating ?? d.Rating, 1);
            d.Notes = NullIfBlank(r.Notes);
        }

        private static DriverDocumentDto ToDto(DriverDocument x)
        {
            var days = (x.ExpiryDate.Date - Today).Days;
            var state = days < 0 ? "Expired" : days <= DriverConstants.ExpiryReminderDays ? "Expiring" : "Valid";
            return new DriverDocumentDto(x.Id, x.Name, x.ExpiryDate, days, state, x.FileKey is not null, x.FileName, x.ContentType);
        }

        private static int? DaysLeft(DateTime? date) => date is null ? null : (date.Value.Date - Today).Days;

        private static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        /// <summary>Checks the file's magic bytes so a renamed executable can't pass as a scan.</summary>
        private static async Task<bool> HasValidSignatureAsync(Stream content, string extension)
        {
            var header = new byte[8];
            var read = await content.ReadAsync(header.AsMemory(0, header.Length));
            if (content.CanSeek) content.Position = 0;
            if (read < 4) return false;

            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                _ => false
            };
        }
    }
}
