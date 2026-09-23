using CRM.Core.Common;
using CRM.Core.DTOs.CompanyDtos;
using CRM.Infrastructure.Persistence.Entities;

namespace CRM.Core.Services
{
    public interface ICompanyService
    {
        Task<IEnumerable<Company>> GetAllAsync();

        Task<Company?> GetByIdAsync(int id);

        Task<Company?> GetByTaxNumberAsync(string taxNumber);

        Task<Company> CreateAsync(Company company);

        Task<Company?> UpdateAsync(int id, UpdateCompanyDto dto);

        Task<bool> DeleteAsync(int id);

        Task<PagedResponse<Company>> GetPagedAsync(PaginationRequest request);

        Task<PagedResponse<Company>> GetFilteredAsync(CompanyFilterRequest request);

        Task<Company?> PatchAsync(int id, UpdateCompanyPatchDto request);

    }
}
