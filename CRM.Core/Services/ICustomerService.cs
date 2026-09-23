using CRM.Core.Common;
using CRM.Core.DTOs.CustomerDtos;
using CRM.Infrastructure.Persistence.Entities;

namespace CRM.Core.Services
{
    public interface ICustomerService
    {
        Task<IEnumerable<Customer>> GetAllAsync();

        Task<Customer?> GetByIdAsync(int id);

        Task<Customer?> GetByEmailAsync(string email);

        Task<Customer> CreateAsync(Customer customer);

        Task<Customer?> UpdateAsync(int id, UpdateCustomerDto dto);

        Task<bool> DeleteAsync(int id);

        Task<PagedResponse<Customer>> GetPagedAsync(PaginationRequest request);

        Task<PagedResponse<Customer>> GetFilteredAsync(CustomerFilterRequest request);

        Task<Customer?> PatchAsync(int id, UpdateCustomerPatchDto request);
    }
}
