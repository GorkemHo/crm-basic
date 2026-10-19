using CRM.Core.Common;
using CRM.Core.DTOs.CompanyDtos;
using CRM.Core.Interfaces;
using CRM.Core.Mappings;
using CRM.Core.Services;
using CRM.Core.Entities;

namespace CRM.Infrastructure.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly ICustomerRepository _customerRepository;

        public CompanyService(
            ICompanyRepository companyRepository,
            ICustomerRepository customerRepository)
        {
            _companyRepository = companyRepository;
            _customerRepository = customerRepository;
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
            await EnsureTaxNumberIsUniqueAsync(company.TaxNumber, excludeId: null);

            return await _companyRepository.AddAsync(company);
        }

        public async Task<Company?> UpdateAsync(int id, UpdateCompanyDto dto)
        {
            var company = await _companyRepository.GetByIdAsync(id);

            if (company is null)
            {
                return null;
            }

            await EnsureTaxNumberIsUniqueAsync(dto.TaxNumber, excludeId: id);

            dto.MapToEntity(company);

            await _companyRepository.UpdateAsync(company);

            return company;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var company = await _companyRepository.GetByIdAsync(id);

            if (company is null)
            {
                return false;
            }

            if (await _customerRepository.ExistsAsync(x => x.CompanyId == id))
            {
                throw new InvalidOperationException(
                "The company has customers and cannot be deleted.");
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
            {
                await EnsureTaxNumberIsUniqueAsync(request.TaxNumber, excludeId: id);
                company.TaxNumber = request.TaxNumber;
            }

            if (request.IsActive.HasValue)
                company.IsActive = request.IsActive.Value;

            company.UpdatedAt = DateTime.UtcNow;

            await _companyRepository.UpdateAsync(company);

            return company;
        }

        private async Task EnsureTaxNumberIsUniqueAsync(string? taxNumber, int? excludeId)
        {
            if (string.IsNullOrWhiteSpace(taxNumber))
            {
                return;
            }

            var existingCompany = await _companyRepository.GetByTaxNumberAsync(taxNumber);

            if (existingCompany is not null && existingCompany.Id != excludeId)
            {
                throw new InvalidOperationException(
                "A company with the same tax number already exists.");
            }
        }
    }
}
