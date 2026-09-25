using Axpense.Data.Constants;
using Axpense.Data.Entities.CatalogEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Common;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Catalog
{
    /// <summary>
    /// Organization parts catalogue: categories (groups) and their preset parts.
    /// Rules: names required (≤ 100), category names unique per organization, part names unique
    /// within their category (case-insensitive). Codes are assigned once and never reused while
    /// the item exists: category C{n}; part P{n}{seq:00}.
    /// </summary>
    public sealed class PartsCatalogService : IPartsCatalogService
    {
        private const int MaxName = 100;
        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;

        public PartsCatalogService(AxpenseDbContext db, IUnitOfWork uow)
        {
            _db = db;
            _uow = uow;
        }

        public async Task<PartsCatalogDto> GetAsync(Guid organizationId, CancellationToken ct = default)
        {
            // Organizations created before the catalogue existed get the defaults on first use.
            await PartsCatalogSeeder.SeedIfEmptyAsync(_db, organizationId, ct);

            var categories = await Categories(organizationId)
                .OrderBy(c => c.Number)
                .Select(c => new
                {
                    c.Id, c.Number, c.Name, c.NameAr,
                    Parts = c.Parts.Where(p => p.CurrentState == (int)CurrentStatusType.Active)
                        .OrderBy(p => p.Sequence)
                        .Select(p => new PresetPartDto(p.Id, p.CategoryId, p.Code, p.Name, p.NameAr))
                        .ToList()
                })
                .ToListAsync(ct);

            var dto = categories.Select(c => new PartCategoryDto(c.Id, $"C{c.Number}", c.Number, c.Name, c.NameAr, c.Parts)).ToList();
            return new PartsCatalogDto(dto, dto.Sum(c => c.Parts.Count));
        }

        // ---------------------------------------------------------------- categories

        public async Task<ServiceResult<PartCategoryDto>> CreateCategoryAsync(Guid organizationId, Guid userId, CatalogNameRequest request, CancellationToken ct = default)
        {
            var (name, nameAr, errors) = Normalize(request, "Category name");
            if (errors.Count > 0) return ServiceResult<PartCategoryDto>.Invalid(errors);
            if (await CategoryNameTakenAsync(organizationId, name, null, ct))
                return ServiceResult<PartCategoryDto>.Conflict("CATEGORY_EXISTS", $"A category named {name} already exists.", "name");

            var number = (await _db.PartCategories.Where(c => c.OrganizationId == organizationId).MaxAsync(c => (int?)c.Number, ct) ?? 0) + 1;
            var category = new PartCategory { OrganizationId = organizationId, Number = number, Name = name, NameAr = nameAr };
            await _uow.Repository<PartCategory>().AddAsyncGetID(category, userId, ct);
            return ServiceResult<PartCategoryDto>.Ok(new PartCategoryDto(category.Id, $"C{number}", number, name, nameAr, []));
        }

        public async Task<ServiceResult<PartCategoryDto>> UpdateCategoryAsync(Guid organizationId, Guid userId, Guid categoryId, CatalogNameRequest request, CancellationToken ct = default)
        {
            var (name, nameAr, errors) = Normalize(request, "Category name");
            if (errors.Count > 0) return ServiceResult<PartCategoryDto>.Invalid(errors);

            var repo = _uow.Repository<PartCategory>();
            var category = await repo.GetByIdAsync(categoryId, ct);
            if (category is null || category.OrganizationId != organizationId || category.CurrentState != (int)CurrentStatusType.Active)
                return ServiceResult<PartCategoryDto>.NotFound("Category not found.");
            if (await CategoryNameTakenAsync(organizationId, name, categoryId, ct))
                return ServiceResult<PartCategoryDto>.Conflict("CATEGORY_EXISTS", $"A category named {name} already exists.", "name");

            category.Name = name;
            category.NameAr = nameAr;
            await repo.UpdateAsync(category, userId, ct);

            var parts = await Parts(organizationId).Where(p => p.CategoryId == categoryId).OrderBy(p => p.Sequence)
                .Select(p => new PresetPartDto(p.Id, p.CategoryId, p.Code, p.Name, p.NameAr)).ToListAsync(ct);
            return ServiceResult<PartCategoryDto>.Ok(new PartCategoryDto(category.Id, $"C{category.Number}", category.Number, name, nameAr, parts));
        }

        public async Task<ServiceResult<bool>> DeleteCategoryAsync(Guid organizationId, Guid categoryId, CancellationToken ct = default)
        {
            if (!await Categories(organizationId).AnyAsync(c => c.Id == categoryId, ct))
                return ServiceResult<bool>.NotFound("Category not found.");
            // Parts are removed by the database cascade — unless one of them is fitted to a vehicle or named on an issue.
            var inUse = await _db.VehicleParts.AsNoTracking().CountAsync(p => p.OrganizationId == organizationId && p.PresetPart.CategoryId == categoryId, ct)
                      + await _db.VehicleIssues.AsNoTracking().CountAsync(i => i.OrganizationId == organizationId && i.PresetPart != null && i.PresetPart.CategoryId == categoryId, ct)
                      + await _db.InspectionTemplateItems.AsNoTracking().CountAsync(i => i.OrganizationId == organizationId && i.PresetPart.CategoryId == categoryId, ct);
            if (inUse > 0)
                return ServiceResult<bool>.Conflict("CATEGORY_IN_USE", $"Parts in this category are used on vehicles or inspection templates ({inUse} record{(inUse == 1 ? "" : "s")}). Remove them from those first.");
            await _uow.Repository<PartCategory>().DeleteAsync(categoryId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // ---------------------------------------------------------------- parts

        public async Task<ServiceResult<PresetPartDto>> CreatePartAsync(Guid organizationId, Guid userId, Guid categoryId, CatalogNameRequest request, CancellationToken ct = default)
        {
            var category = await Categories(organizationId).FirstOrDefaultAsync(c => c.Id == categoryId, ct);
            if (category is null) return ServiceResult<PresetPartDto>.NotFound("Category not found.");

            var (name, nameAr, errors) = Normalize(request, "Part name");
            if (errors.Count > 0) return ServiceResult<PresetPartDto>.Invalid(errors);
            if (await PartNameTakenAsync(organizationId, categoryId, name, null, ct))
                return ServiceResult<PresetPartDto>.Conflict("PART_EXISTS", $"{name} already exists in {category.Name}.", "name");

            var sequence = (await _db.PresetParts.Where(p => p.OrganizationId == organizationId && p.CategoryId == categoryId)
                .MaxAsync(p => (int?)p.Sequence, ct) ?? 0) + 1;
            var part = new PresetPart
            {
                OrganizationId = organizationId,
                CategoryId = categoryId,
                Sequence = sequence,
                Code = PartsCatalogDefaults.PartCode(category.Number, sequence),
                Name = name,
                NameAr = nameAr
            };
            await _uow.Repository<PresetPart>().AddAsyncGetID(part, userId, ct);
            return ServiceResult<PresetPartDto>.Ok(new PresetPartDto(part.Id, categoryId, part.Code, name, nameAr));
        }

        public async Task<ServiceResult<PresetPartDto>> UpdatePartAsync(Guid organizationId, Guid userId, Guid partId, CatalogNameRequest request, CancellationToken ct = default)
        {
            var (name, nameAr, errors) = Normalize(request, "Part name");
            if (errors.Count > 0) return ServiceResult<PresetPartDto>.Invalid(errors);

            var repo = _uow.Repository<PresetPart>();
            var part = await repo.GetByIdAsync(partId, ct);
            if (part is null || part.OrganizationId != organizationId || part.CurrentState != (int)CurrentStatusType.Active)
                return ServiceResult<PresetPartDto>.NotFound("Part not found.");
            if (await PartNameTakenAsync(organizationId, part.CategoryId, name, partId, ct))
                return ServiceResult<PresetPartDto>.Conflict("PART_EXISTS", $"{name} already exists in this category.", "name");

            part.Name = name;
            part.NameAr = nameAr;
            await repo.UpdateAsync(part, userId, ct);
            return ServiceResult<PresetPartDto>.Ok(new PresetPartDto(part.Id, part.CategoryId, part.Code, name, nameAr));
        }

        public async Task<ServiceResult<bool>> DeletePartAsync(Guid organizationId, Guid partId, CancellationToken ct = default)
        {
            if (!await Parts(organizationId).AnyAsync(p => p.Id == partId, ct))
                return ServiceResult<bool>.NotFound("Part not found.");
            var inUse = await _db.VehicleParts.AsNoTracking().CountAsync(p => p.OrganizationId == organizationId && p.PresetPartId == partId, ct)
                      + await _db.VehicleIssues.AsNoTracking().CountAsync(i => i.OrganizationId == organizationId && i.PresetPartId == partId, ct)
                      + await _db.InspectionTemplateItems.AsNoTracking().CountAsync(i => i.OrganizationId == organizationId && i.PresetPartId == partId, ct);
            if (inUse > 0)
                return ServiceResult<bool>.Conflict("PART_IN_USE", $"This part is used on vehicles or inspection templates ({inUse} record{(inUse == 1 ? "" : "s")}). Remove it from those first.");
            await _uow.Repository<PresetPart>().DeleteAsync(partId, ct);
            return ServiceResult<bool>.Ok(true);
        }

        // ---------------------------------------------------------------- helpers

        private IQueryable<PartCategory> Categories(Guid organizationId) =>
            _db.PartCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.CurrentState == (int)CurrentStatusType.Active);

        private IQueryable<PresetPart> Parts(Guid organizationId) =>
            _db.PresetParts.AsNoTracking().Where(p => p.OrganizationId == organizationId && p.CurrentState == (int)CurrentStatusType.Active);

        private Task<bool> CategoryNameTakenAsync(Guid organizationId, string name, Guid? exceptId, CancellationToken ct)
        {
            var lower = name.ToLower();
            return _db.PartCategories.AsNoTracking().AnyAsync(c =>
                c.OrganizationId == organizationId && c.Name.ToLower() == lower && (exceptId == null || c.Id != exceptId), ct);
        }

        private Task<bool> PartNameTakenAsync(Guid organizationId, Guid categoryId, string name, Guid? exceptId, CancellationToken ct)
        {
            var lower = name.ToLower();
            return _db.PresetParts.AsNoTracking().AnyAsync(p =>
                p.OrganizationId == organizationId && p.CategoryId == categoryId && p.Name.ToLower() == lower && (exceptId == null || p.Id != exceptId), ct);
        }

        private static (string Name, string? NameAr, Dictionary<string, string> Errors) Normalize(CatalogNameRequest r, string label)
        {
            var errors = new Dictionary<string, string>();
            var name = r.Name?.Trim() ?? "";
            var nameAr = string.IsNullOrWhiteSpace(r.NameAr) ? null : r.NameAr.Trim();
            if (name.Length == 0) errors["name"] = $"{label} is required.";
            else if (name.Length > MaxName) errors["name"] = $"{label} must be {MaxName} characters or fewer.";
            if (nameAr?.Length > MaxName) errors["nameAr"] = $"Arabic name must be {MaxName} characters or fewer.";
            return (name, nameAr, errors);
        }
    }
}
