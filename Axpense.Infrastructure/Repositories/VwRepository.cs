using Axpense.Data.Exceptions;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Intertfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Axpense.Infrastructure.Repositories
{
    public class VwRepository<T> : IVwRepository<T> where T : class
    {
        private readonly AxpenseDbContext _context;
        private readonly DbSet<T> _dbSet;
        private readonly ILogger<VwRepository<T>> _logger;

        public VwRepository(AxpenseDbContext context, ILogger<VwRepository<T>> Logger)
        {
            _context = context;
            _dbSet = _context.Set<T>();
            _logger = Logger;
        }
        public async Task<List<T>> GetAllAsync()
        {
            try
            {
                return await _dbSet.ToListAsync();
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get View List", _logger);
            }
        }
        public async Task<T?> GetByIdAsync(Guid id)
        {
            try
            {
                return await _dbSet.FindAsync(id).AsTask();
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get Row by Id from View.", _logger);
            }
        }
        public async Task<T?> GetFirstOrDefault(Expression<Func<T, bool>> Filter)
        {
            try
            {
                return await _dbSet.FirstOrDefaultAsync(Filter);
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get First Or Default from Cities View.", _logger);
            }
        }
        public async Task<List<T>> GetList(Expression<Func<T, bool>> Filter)
        {
            try
            {
                return await _dbSet.Where(Filter).ToListAsync();
            }
            catch (Exception EX)
            {
                throw new DataAccessException(EX, "Error To Get List from Cities View.", _logger);
            }
        }
    }
}
