using Axpense.Data.Constants;
using Axpense.Data.Entities.SaasEntities;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Common;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Settings
{
    public interface IExpenseTypeService
    {
        Task<IReadOnlyList<ExpenseTypeDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<ExpenseTypeDto>> CreateAsync(Guid organizationId, Guid userId, ExpenseTypeRequest request, CancellationToken ct = default);
        Task<ServiceResult<ExpenseTypeDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, ExpenseTypeRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    }

    /// <summary>
    /// Expense types. Rules: unique name (case-insensitive, ≤ 60); colour from the palette or a hex
    /// value; system types (Fuel, Maintenance, Other) can be renamed/recoloured but not deleted;
    /// a type that has expenses can't be deleted; renaming a type renames the category on its expenses.
    /// </summary>
    public sealed class ExpenseTypeService : IExpenseTypeService
    {
        private const int MaxName = 60;
        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;

        public ExpenseTypeService(AxpenseDbContext db, IUnitOfWork uow)
        {
            _db = db;
            _uow = uow;
        }

        public async Task<IReadOnlyList<ExpenseTypeDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
        {
            await OrganizationDefaultsSeeder.SeedExpenseTypesAsync(_db, organizationId, ct);
            var types = await SpendQueries.ActiveTypesAsync(_db, organizationId, ct);
            var spend = await SpendQueries.SpendAsync(_db, organizationId, types, null, null, ct);
            var byType = spend.GroupBy(r => r.ExpenseTypeId).ToDictionary(g => g.Key ?? Guid.Empty, g => (Count: g.Count(), Total: g.Sum(r => r.Amount)));
            return types.Select(t =>
            {
                var s = byType.GetValueOrDefault(t.Id);
                return new ExpenseTypeDto(t.Id, t.Name, t.Color, t.SystemKey != null, s.Count, s.Total);
            }).ToList();
        }

        public async Task<ServiceResult<ExpenseTypeDto>> CreateAsync(Guid organizationId, Guid userId, ExpenseTypeRequest request, CancellationToken ct = default)
        {
            var errors = SettingsValidation.Name(request.Name, "Type name", MaxName, out var name);
            var color = NormalizeColor(request.Color, errors);
            if (errors.Count > 0) return ServiceResult<ExpenseTypeDto>.Invalid(errors);
            if (await NameTakenAsync(organizationId, name, null, ct))
                return ServiceResult<ExpenseTypeDto>.Conflict("EXPENSE_TYPE_EXISTS", $"An expense type named {name} already exists.", "name");

            var count = await _db.ExpenseTypes.CountAsync(t => t.OrganizationId == organizationId, ct);
            var order = (await _db.ExpenseTypes.Where(t => t.OrganizationId == organizationId).MaxAsync(t => (int?)t.SortOrder, ct) ?? 0) + 1;
            var type = new ExpenseType
            {
                OrganizationId = organizationId,
                Name = name,
                Color = color ?? SettingsDefaults.Palette[count % SettingsDefaults.Palette.Length],
                SortOrder = order
            };
            await _uow.Repository<ExpenseType>().AddAsyncGetID(type, userId, ct);
            return ServiceResult<ExpenseTypeDto>.Ok(new ExpenseTypeDto(type.Id, type.Name, type.Color, false, 0, 0));
        }

        public async Task<ServiceResult<ExpenseTypeDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, ExpenseTypeRequest request, CancellationToken ct = default)
        {
            var errors = SettingsValidation.Name(request.Name, "Type name", MaxName, out var name);
            var color = NormalizeColor(request.Color, errors);
            if (errors.Count > 0) return ServiceResult<ExpenseTypeDto>.Invalid(errors);

            var type = await _db.ExpenseTypes.FirstOrDefaultAsync(t =>
                t.Id == id && t.OrganizationId == organizationId && t.CurrentState == (int)CurrentStatusType.Active, ct);
            if (type is null) return ServiceResult<ExpenseTypeDto>.NotFound("Expense type not found.");
            if (await NameTakenAsync(organizationId, name, id, ct))
                return ServiceResult<ExpenseTypeDto>.Conflict("EXPENSE_TYPE_EXISTS", $"An expense type named {name} already exists.", "name");

            var oldName = type.Name;
            var oldLower = oldName.ToLower();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            if (!string.Equals(type.Name, name, StringComparison.Ordinal))
            {
                // Expenses still reference their type by name — keep them attached to the renamed type.
                var rows = await _db.Expenses.Where(x => x.OrganizationId == organizationId && x.Category.ToLower() == oldLower).ToListAsync(ct);
                foreach (var r in rows) r.Category = name;
            }
            type.Name = name;
            if (color is not null) type.Color = color;
            type.UpdatedBy = userId;
            type.UpdatedAt = DateTime.UtcNow;
            _db.AuditLogs.Add(new AuditLog
            {
                OrganizationId = organizationId, UserId = userId, Action = "Updated", EntityType = nameof(ExpenseType),
                EntityId = id.ToString(), Details = oldName == name ? "Colour changed" : $"Renamed {oldName} → {name}", CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dto = (await ListAsync(organizationId, ct)).First(t => t.Id == id);
            return ServiceResult<ExpenseTypeDto>.Ok(dto);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            var list = await ListAsync(organizationId, ct);
            var type = list.FirstOrDefault(t => t.Id == id);
            if (type is null) return ServiceResult<bool>.NotFound("Expense type not found.");
            if (type.IsSystem)
                return ServiceResult<bool>.Conflict("EXPENSE_TYPE_SYSTEM", $"{type.Name} is a system type and can't be deleted.");
            if (type.Entries > 0)
                return ServiceResult<bool>.Conflict("EXPENSE_TYPE_IN_USE",
                    $"{type.Name} has {type.Entries} expense{(type.Entries == 1 ? "" : "s")}. Move them to another type before deleting it.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.BudgetCategoryShares.Where(s => s.ExpenseTypeId == id).ExecuteDeleteAsync(ct);
            await _uow.Repository<ExpenseType>().DeleteAsync(id, ct);
            await tx.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        private Task<bool> NameTakenAsync(Guid organizationId, string name, Guid? exceptId, CancellationToken ct)
        {
            var lower = name.ToLower();
            return _db.ExpenseTypes.AnyAsync(t => t.OrganizationId == organizationId && t.Name.ToLower() == lower && (exceptId == null || t.Id != exceptId), ct);
        }

        private static string? NormalizeColor(string? color, Dictionary<string, string> errors)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;
            var c = color.Trim();
            var valid = c.Length == 7 && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit);
            if (!valid) errors["color"] = "Colour must be a hex value like #19B394.";
            return valid ? c.ToUpperInvariant() : null;
        }
    }
}
