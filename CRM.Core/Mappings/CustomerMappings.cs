using CRM.Core.DTOs.CustomerDtos;
using CRM.Infrastructure.Persistence.Entities;

namespace CRM.Core.Mappings
{
    public static class CustomerMappings
    {
        public static CustomerDto ToDto(this Customer customer)
        {
            return new CustomerDto
            {
                Id = customer.Id,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                CompanyId = customer.CompanyId,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt,
                UpdatedAt = customer.UpdatedAt
            };
        }

        public static Customer ToEntity(this CreateCustomerDto dto)
        {
            return new Customer
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                CompanyId = dto.CompanyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static void MapToEntity(
        this UpdateCustomerDto dto,
        Customer customer)
        {
            customer.FirstName = dto.FirstName;
            customer.LastName = dto.LastName;
            customer.Email = dto.Email;
            customer.PhoneNumber = dto.PhoneNumber;
            customer.CompanyId = dto.CompanyId;
            customer.IsActive = dto.IsActive;
            customer.UpdatedAt = DateTime.UtcNow;
        }
    }
}
