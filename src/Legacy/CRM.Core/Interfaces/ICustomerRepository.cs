using CRM.Core.Common;
using CRM.Core.Entities;

namespace CRM.Core.Interfaces
{
    public interface ICustomerRepository : IRepository<Customer>
    {
        Task<Customer?> GetByEmailAsync(string email);

        Task<PagedResponse<Customer>> GetFilteredAsync(CustomerFilterRequest request);
    }
}
