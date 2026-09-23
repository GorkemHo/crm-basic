using CRM.Core.Common;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Persistence.Context;
using CRM.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Persistence.Repositories
{
    public class CustomerRepository : Repository<Customer>, ICustomerRepository
    {
        public CustomerRepository(CrmDbContext context) : base(context)
        {
        }

        public async Task<Customer?> GetByEmailAsync(string email)
        {
            return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<PagedResponse<Customer>> GetFilteredAsync(CustomerFilterRequest request)
        {
            IQueryable<Customer> query = _dbSet.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();

                query = query.Where(x => x.FirstName.ToLower().Contains(search) ||
                                        x.LastName.ToLower().Contains(search) ||
                                        x.Email.ToLower().Contains(search));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (request.CompanyId.HasValue)
            {
                query = query.Where(x =>
                x.CompanyId == request.CompanyId.Value);
            }

            query = query.ApplySorting(request.SortBy, request.SortDirection);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return new PagedResponse<Customer>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }
}
