using CRM.Core.Common;
using System.Linq.Expressions;

namespace CRM.Infrastructure.Persistence.Repositories
{
    public interface IRepository<TEntity>
    {
        Task<TEntity?> GetByIdAsync(int id);

        Task<IEnumerable<TEntity>> GetAllAsync();

        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

        Task<TEntity> AddAsync(TEntity entity);

        Task UpdateAsync(TEntity entity);

        Task DeleteAsync(TEntity entity);

        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate);

        Task<PagedResponse<TEntity>> GetPagedAsync(int pageNumber, int pageSize);
    }
}
