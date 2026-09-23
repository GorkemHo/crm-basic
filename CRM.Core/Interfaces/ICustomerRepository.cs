using CRM.Core.Common;
using CRM.Infrastructure.Persistence.Entities;
using CRM.Infrastructure.Persistence.Repositories;

namespace CRM.Core.Interfaces
{
    public interface ICustomerRepository : IRepository<Customer>
    {
        Task<Customer?> GetByEmailAsync(string email);

        Task<PagedResponse<Customer>> GetFilteredAsync(CustomerFilterRequest request);
    }
}
