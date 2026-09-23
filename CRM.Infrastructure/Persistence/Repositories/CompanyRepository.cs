using CRM.Core.Common;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Persistence.Context;
using CRM.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Persistence.Repositories
{
    public class CompanyRepository : Repository<Company>, ICompanyRepository
    {
        public CompanyRepository(CrmDbContext context)
        : base(context)
        {
        }

        public async Task<Company?> GetByTaxNumberAsync(string taxNumber)
        {
            return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TaxNumber == taxNumber);
        }

        public async Task<PagedResponse<Company>> GetFilteredAsync(CompanyFilterRequest request)
        {
            IQueryable<Company> query = _dbSet.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                query = query.Where(x => x.CompanyName.Contains(request.Search));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(x => x.IsActive == request.IsActive.Value);
            }

            query = query.ApplySorting(request.SortBy, request.SortDirection);

            var totalCount = await query.CountAsync();

            var items = await query
                        .Skip((request.PageNumber - 1) * request.PageSize)
                        .Take(request.PageSize)
                        .ToListAsync();

            return new PagedResponse<Company>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }
}
