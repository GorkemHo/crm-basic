using CRM.Core.Common;
using CRM.Infrastructure.Persistence.Entities;
using CRM.Infrastructure.Persistence.Repositories;

namespace CRM.Core.Interfaces
{
    public interface ICompanyRepository : IRepository<Company>
    {
        public Task<Company?> GetByTaxNumberAsync(string taxNumber);

        Task<PagedResponse<Company>> GetFilteredAsync(CompanyFilterRequest request);
    }
}
