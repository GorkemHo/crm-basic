using CRM.Core.Common;
using CRM.Core.DTOs.CompanyDtos;
using CRM.Core.Interfaces;
using CRM.Core.Mappings;
using CRM.Infrastructure.Persistence.Entities;

namespace CRM.Core.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;

        public CompanyService(ICompanyRepository companyRepository)
        {
            _companyRepository = companyRepository;
        }

        public async Task<IEnumerable<Company>> GetAllAsync()
        {
            return await _companyRepository.GetAllAsync();
        }

        public async Task<Company?> GetByIdAsync(int id)
        {
            return await _companyRepository.GetByIdAsync(id);
        }

        public async Task<Company?> GetByTaxNumberAsync(string taxNumber)
        {
            return await _companyRepository.GetByTaxNumberAsync(taxNumber);
        }

        public async Task<Company> CreateAsync(Company company)
        {
            var existingCompany =
            await _companyRepository.GetByTaxNumberAsync(company.TaxNumber!);

            if (existingCompany is not null)
            {
                throw new InvalidOperationException(
                "A company with the same tax number already exists.");
            }

            return await _companyRepository.AddAsync(company);
        }

        public async Task<Company?> UpdateAsync(int id, UpdateCompanyDto dto)
        {
            var company = await _companyRepository.GetByIdAsync(id);

            if (company is null)
            {
                return null;
            }

            dto.MapToEntity(company!);

            await _companyRepository.UpdateAsync(company!);

            return company;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var company = await _companyRepository.GetByIdAsync(id);

            if (company is null)
            {
                return false;
            }

            await _companyRepository.DeleteAsync(company);

            return true;
        }

        public async Task<PagedResponse<Company>> GetPagedAsync(PaginationRequest request)
        {
            return await _companyRepository.GetPagedAsync(request.PageNumber, request.PageSize);
        }

        public async Task<PagedResponse<Company>> GetFilteredAsync(CompanyFilterRequest request)
        {
            return await _companyRepository
                .GetFilteredAsync(request);
        }

        public async Task<Company?> PatchAsync(
    int id,
    UpdateCompanyPatchDto request)
        {
            var company = await _companyRepository
                .GetByIdAsync(id);

            if (company == null)
                return null;


            if (request.CompanyName != null)
                company.CompanyName = request.CompanyName;


            if (request.TaxNumber != null)
                company.TaxNumber = request.TaxNumber;


            if (request.IsActive.HasValue)
                company.IsActive = request.IsActive.Value;


            await _companyRepository.UpdateAsync(company);


            return company;
        }
    }
}
