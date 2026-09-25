using Axpense.Data.Entities;
using Axpense.Data.Enums;
using Axpense.Data.PageModel;
using System.Linq.Expressions;

namespace Axpense.Infrastructure.Intertfaces
{
    public interface IGenericRepository<T> where T : BaseEntity
    {
        Task<List<T>> GetAllAsync();

        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> AddAsync(T entity, Guid USerID, CancellationToken cancellationToken = default);

        Task<(bool Success, Guid EntityId)> AddAsyncGetID(T entity, Guid userId, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        Task<int> UpdateAsync(T entity, Guid USerID, CancellationToken cancellationToken = default);

        Task<int> ChangeStatus(Guid id, Guid USerID, int Status = (int)CurrentStatusType.Active, CancellationToken cancellationToken = default);

        Task<T> GetFirstOrDefault(Expression<Func<T, bool>> Filter, CancellationToken cancellationToken = default);

        Task<List<T>> GetListAsync(Expression<Func<T, bool>> Filter, CancellationToken cancellationToken = default);
        Task<Tresult?> GetByIdAsync<Tresult>(
            Expression<Func<T, bool>> filter,
            Expression<Func<T, Tresult>> selector,
            Expression<Func<T, object>>? orderBy = null,
            bool isDescending = false,
            CancellationToken cancellationToken = default);

        Task<PagedResult<Tresult>> GetPagedList<Tresult>(
            Expression<Func<T, bool>>? filter,
            Expression<Func<T, Tresult>> selector,
            Expression<Func<T, object>> orderBy,
            int pageNumber = 1,
            int pageSize = 10,
            bool isDescending = false,
            CancellationToken cancellationToken = default);

        Task<List<Tresult>> GetListAsync<Tresult>(
             Expression<Func<T, bool>> filter,
             Expression<Func<T, Tresult>> selector,
             Expression<Func<T, object>> orderBy = null,
             bool isDescending = false,
             CancellationToken cancellationToken = default);

        Task<bool> UpdateFieldsync(Guid id, Action<T> updateAction, CancellationToken cancellationToken = default);

    }

}
