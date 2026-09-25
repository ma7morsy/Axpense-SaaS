using Axpense.Data.Entities;
using Axpense.Data.Entities.SaasEntities;
using Axpense.Data.Enums;
using Axpense.Data.Exceptions;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using Axpense.Data.PageModel;

namespace Axpense.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        #region filds
        private readonly AxpenseDbContext _context;
        private readonly DbSet<T> _dbSet;
        private readonly ILogger<GenericRepository<T>> _logger;
        #endregion

        #region Constructor 
        public GenericRepository(AxpenseDbContext context, ILogger<GenericRepository<T>> Logger)
        {
            _context = context;
            _dbSet = _context.Set<T>();
            _logger = Logger;
        }
        #endregion

        #region Methods
        public async Task<List<T>> GetAllAsync()
        {
            try
            {
                return await _dbSet.Where(x => x.CurrentState == (int)CurrentStatusType.Active).ToListAsync();
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get Entities List", _logger);
            }
        }
        public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.FindAsync(new object?[] { id }, cancellationToken);
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get Row by Id from Entity Table", _logger);
            }
        }
        public async Task<bool> AddAsync(T entity, Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.CreatedBy = userId;
                entity.CurrentState = (int)CurrentStatusType.Active;

                await _dbSet.AddAsync(entity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                LogAndAudit("Created", entity.Id, userId);
                return true;
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Add Row To Entity Table", _logger);
            }
        }
        public async Task<(bool Success, Guid EntityId)> AddAsyncGetID(T entity, Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.CreatedBy = userId;
                entity.CurrentState = (int)CurrentStatusType.Active;

                await _dbSet.AddAsync(entity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                LogAndAudit("Created", entity.Id, userId);
                return (true, entity.Id);
            }
            catch (Exception ex)
            {
                throw new DataAccessException(ex, "Error To Add Row To Entity Table", _logger);
            }
        }
        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _dbSet.FindAsync(new object?[] { id }, cancellationToken);
                if (entity != null)
                {
                    _dbSet.Remove(entity);
                    LogAndAudit("Deleted", id, Guid.Empty);
                    await _context.SaveChangesAsync(cancellationToken);
                    return true;
                }
                return false;
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Delete Row From Entity Table", _logger);
            }
        }
        public async Task<int> UpdateAsync(T entity, Guid USerID, CancellationToken cancellationToken = default)
        {
            try
            {
                var entDb = await _dbSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);

                if (entDb == null)
                {
                    return -1;
                }

                entity.CreatedAt = entDb.CreatedAt;
                entity.CreatedBy = entDb.CreatedBy;
                entity.UpdatedBy = USerID;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.CurrentState = entDb.CurrentState;

                _dbSet.Update(entity);
                LogAndAudit("Updated", entity.Id, USerID);
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Update Row From Entity Table", _logger);
            }
        }
        public async Task<int> ChangeStatus(Guid id, Guid USerID, int Status = (int)CurrentStatusType.Active, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _dbSet.FindAsync(new object?[] { id }, cancellationToken);
                if (entity == null)
                {
                    throw new DataAccessException(
                        new KeyNotFoundException($"Entity with id {id} was not found."),
                        "Error changing entity status",
                        _logger);
                }

                entity.CurrentState = Status;
                entity.UpdatedBy = USerID;
                entity.UpdatedAt = DateTime.Now;

                _context.Entry(entity).State = EntityState.Modified;
                LogAndAudit("StatusChanged", id, USerID, $"NewState={Status}");
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, $"Error changing status for entity with id {id}", _logger);
            }

        }
        public async Task<T> GetFirstOrDefault(Expression<Func<T, bool>> Filter, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.AsNoTracking().Where(Filter).FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get Row by Filter from Entity Table", _logger);
            }
        }
        public async Task<List<T>> GetListAsync(Expression<Func<T, bool>> Filter, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.AsNoTracking().Where(Filter).ToListAsync(cancellationToken);
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get Row by Filter from Entity Table", _logger);
            }
        }
        public async Task<Tresult?> GetByIdAsync<Tresult>(
           Expression<Func<T, bool>> filter,
           Expression<Func<T, Tresult>> selector,
           Expression<Func<T, object>>? orderBy = null,
           bool isDescending = false,
           CancellationToken cancellationToken = default)
        {
            try
            {
                IQueryable<T> query = _dbSet.AsNoTracking();

                if (filter != null)
                    query = query.Where(filter);

                if (orderBy != null)
                    query = isDescending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);

                return await query.Select(selector).FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new DataAccessException(ex, "Error To Get Item by Filter from Entity Table", _logger);
            }
        }
        public async Task<PagedResult<Tresult>> GetPagedList<Tresult>(
            Expression<Func<T, bool>>? filter,
            Expression<Func<T, Tresult>> selector,
            Expression<Func<T, object>> orderBy,
            int pageNumber = 1,
            int pageSize = 10,
            bool isDescending = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                IQueryable<T> query = _dbSet.AsNoTracking();

                if (filter != null)
                    query = query.Where(filter);

                query = isDescending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 10;

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(selector)
                    .ToListAsync(cancellationToken);

                return new PagedResult<Tresult>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                throw new DataAccessException(ex, "Error To Get Paged List by Filter from Entity Table", _logger);
            }
        }
        public async Task<List<Tresult>> GetListAsync<Tresult>(
            Expression<Func<T, bool>> filter,
            Expression<Func<T, Tresult>> selector,
            Expression<Func<T, object>>? orderBy = null,
            bool isDescending = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                IQueryable<T> query = _dbSet.AsNoTracking();

                if (filter != null)
                    query = query.Where(filter);

                if (orderBy != null)
                    query = isDescending
                        ? query.OrderByDescending(orderBy)
                        : query.OrderBy(orderBy);

                return await query.Select(selector).ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new DataAccessException(ex, "Error To Get List by Filter from Entity Table", _logger);
            }
        }
        public async Task<bool> UpdateFieldsync(Guid id, Action<T> updateAction, CancellationToken cancellationToken = default)
        {
            try

            {
                // Get entity by id
                var entity = _dbSet.FirstOrDefault(e => e.Id == id);

                // If entity not found
                if (entity == null)
                    return false;

                // Apply updates from outside
                updateAction(entity);

                // Mark entity as modified
                _context.Entry(entity).State = EntityState.Modified;

                // Save changes
                _context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                throw new DataAccessException(ex, "Error To Update Field in Entity Table", _logger);
            }
        }
        private void LogAndAudit(string action, Guid entityId, Guid userId, string? details = null)
        {
            _logger.LogInformation(
                "{Action} {Entity}#{EntityId} by user {UserId}. {Details}",
                action, typeof(T).Name, entityId, userId, details);

            // الإنشاء مالوش صفّ Audit: السجل نفسه فيه CreatedBy/CreatedAt = نفس المعلومة.
            // الأوديت للتعديل/الحذف/تغيير الحالة بس (دي اللي الكيان مابيحتفظش بتاريخها).
            if (action == "Created")
                return;

            var organizationId = (_dbSet.Local.FirstOrDefault(e => e.Id == entityId) as TenantEntity)?.OrganizationId
                ?? Guid.Empty;

            _context.Set<AuditLog>().Add(new AuditLog
            {
                OrganizationId = organizationId,
                EntityType = typeof(T).Name,
                EntityId = entityId.ToString(),
                Action = action,
                UserId = userId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });
        }
        #endregion
    }
}
