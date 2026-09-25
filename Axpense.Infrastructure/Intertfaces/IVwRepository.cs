using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Axpense.Infrastructure.Intertfaces
{
    public interface IVwRepository<T> where T : class
    {
        Task<List<T>> GetAllAsync();
        Task<T?> GetByIdAsync(Guid id);
        Task<T?> GetFirstOrDefault(Expression<Func<T, bool>> Filter);
        Task<List<T>> GetList(Expression<Func<T, bool>> Filter);
    }
}
