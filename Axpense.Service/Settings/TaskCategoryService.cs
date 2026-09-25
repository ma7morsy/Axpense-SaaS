using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Enums;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Common;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Service.Settings
{
    public interface ITaskCategoryService
    {
        Task<IReadOnlyList<TaskCategoryDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
        Task<ServiceResult<TaskCategoryDto>> CreateAsync(Guid organizationId, Guid userId, TaskCategoryRequest request, CancellationToken ct = default);
        Task<ServiceResult<TaskCategoryDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, TaskCategoryRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    }

    /// <summary>
    /// Work-order task categories. Unique name per organization (≤ 60). Task counts are 0 until the
    /// Work orders module links tasks to a category; a category in use will then be protected from deletion.
    /// </summary>
    public sealed class TaskCategoryService : ITaskCategoryService
    {
        private const int MaxName = 60;
        private readonly AxpenseDbContext _db;
        private readonly IUnitOfWork _uow;

        public TaskCategoryService(AxpenseDbContext db, IUnitOfWork uow)
        {
            _db = db;
            _uow = uow;
        }

        public async Task<IReadOnlyList<TaskCategoryDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
        {
            await OrganizationDefaultsSeeder.SeedTaskCategoriesAsync(_db, organizationId, ct);
            return await Active(organizationId).OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => new TaskCategoryDto(c.Id, c.Name, _db.MaintenanceTasks.Count(t => t.OrganizationId == organizationId && t.TaskCategoryId == c.Id && t.CurrentState == (int)Axpense.Data.Enums.CurrentStatusType.Active))).ToListAsync(ct);
        }

        public async Task<ServiceResult<TaskCategoryDto>> CreateAsync(Guid organizationId, Guid userId, TaskCategoryRequest request, CancellationToken ct = default)
        {
            var errors = SettingsValidation.Name(request.Name, "Category name", MaxName, out var name);
            if (errors.Count > 0) return ServiceResult<TaskCategoryDto>.Invalid(errors);
            if (await NameTakenAsync(organizationId, name, null, ct))
                return ServiceResult<TaskCategoryDto>.Conflict("TASK_CATEGORY_EXISTS", $"A task category named {name} already exists.", "name");

            var order = (await _db.TaskCategories.Where(c => c.OrganizationId == organizationId).MaxAsync(c => (int?)c.SortOrder, ct) ?? 0) + 1;
            var category = new TaskCategory { OrganizationId = organizationId, Name = name, SortOrder = order };
            await _uow.Repository<TaskCategory>().AddAsyncGetID(category, userId, ct);
            return ServiceResult<TaskCategoryDto>.Ok(new TaskCategoryDto(category.Id, name, 0));
        }

        public async Task<ServiceResult<TaskCategoryDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, TaskCategoryRequest request, CancellationToken ct = default)
        {
            var errors = SettingsValidation.Name(request.Name, "Category name", MaxName, out var name);
            if (errors.Count > 0) return ServiceResult<TaskCategoryDto>.Invalid(errors);

            var repo = _uow.Repository<TaskCategory>();
            var category = await repo.GetByIdAsync(id, ct);
            if (category is null || category.OrganizationId != organizationId || category.CurrentState != (int)CurrentStatusType.Active)
                return ServiceResult<TaskCategoryDto>.NotFound("Task category not found.");
            if (await NameTakenAsync(organizationId, name, id, ct))
                return ServiceResult<TaskCategoryDto>.Conflict("TASK_CATEGORY_EXISTS", $"A task category named {name} already exists.", "name");

            category.Name = name;
            await repo.UpdateAsync(category, userId, ct);
            return ServiceResult<TaskCategoryDto>.Ok(new TaskCategoryDto(id, name, 0));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            if (!await Active(organizationId).AnyAsync(c => c.Id == id, ct))
                return ServiceResult<bool>.NotFound("Task category not found.");
            await _uow.Repository<TaskCategory>().DeleteAsync(id, ct);
            return ServiceResult<bool>.Ok(true);
        }

        private IQueryable<TaskCategory> Active(Guid organizationId) =>
            _db.TaskCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.CurrentState == (int)CurrentStatusType.Active);

        private Task<bool> NameTakenAsync(Guid organizationId, string name, Guid? exceptId, CancellationToken ct)
        {
            var lower = name.ToLower();
            return _db.TaskCategories.AnyAsync(c => c.OrganizationId == organizationId && c.Name.ToLower() == lower && (exceptId == null || c.Id != exceptId), ct);
        }
    }
}
