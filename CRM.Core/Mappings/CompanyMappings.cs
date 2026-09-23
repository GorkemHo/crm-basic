using CRM.Core.DTOs.CompanyDtos;
using CRM.Infrastructure.Persistence.Entities;

namespace CRM.Core.Mappings
{
    public static class CompanyMappings
    {
        public static CompanyDto ToDto(this Company company)
        {
            return new CompanyDto
            {
                Id = company.Id,
                CompanyName = company.CompanyName,
                TaxNumber = company.TaxNumber,
                IsActive = company.IsActive,
                CreatedAt = company.CreatedAt,
                UpdatedAt = company.UpdatedAt
            };
        }

        public static Company ToEntity(this CreateCompanyDto dto)
        {
            return new Company
            {
                CompanyName = dto.CompanyName,
                TaxNumber = dto.TaxNumber,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static void MapToEntity(this UpdateCompanyDto dto, Company company)
        {
            company.CompanyName = dto.CompanyName;
            company.TaxNumber = dto.TaxNumber;
            company.IsActive = dto.IsActive;
            company.UpdatedAt = DateTime.UtcNow;
        }
    }
}
