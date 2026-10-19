using CRM.Core.Common;
using CRM.Core.Entities;

namespace CRM.Core.Interfaces
{
    public interface ICompanyRepository : IRepository<Company>
    {
        public Task<Company?> GetByTaxNumberAsync(string taxNumber);

        Task<PagedResponse<Company>> GetFilteredAsync(CompanyFilterRequest request);
    }
}
