using CRM.Core.Common;
using CRM.Core.DTOs.CustomerDtos;
using CRM.Core.Interfaces;
using CRM.Core.Mappings;
using CRM.Core.Services;
using CRM.Infrastructure.Persistence.Entities;

namespace CRM.Infrastructure.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            return await _customerRepository.GetAllAsync();
        }

        public async Task<Customer?> GetByIdAsync(int id)
        {
            return await _customerRepository.GetByIdAsync(id);
        }

        public async Task<Customer?> GetByEmailAsync(string email)
        {
            return await _customerRepository.GetByEmailAsync(email);
        }

        public async Task<Customer> CreateAsync(Customer customer)
        {
            var existingCustomer =
            await _customerRepository.GetByEmailAsync(customer.Email);

            if(existingCustomer is not null)
            {
                throw new InvalidOperationException(
                "A customer with the same email already exists.");
            }

            return await _customerRepository.AddAsync(customer);
        }

        public async Task<Customer?> UpdateAsync(
        int id,
        UpdateCustomerDto dto)
        {
            var customer = await _customerRepository.GetByIdAsync(id);

            if(customer is null)
            {
                return null;
            }

            dto.MapToEntity(customer);

            await _customerRepository.UpdateAsync(customer);

            return customer;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var customer = await _customerRepository.GetByIdAsync(id);

            if(customer is null)
            {
                return false;
            }

            await _customerRepository.DeleteAsync(customer);

            return true;
        }

        public async Task<PagedResponse<Customer>> GetPagedAsync(PaginationRequest request)
        {
            return await _customerRepository.GetPagedAsync(request.PageNumber, request.PageSize);
        }

        public async Task<PagedResponse<Customer>> GetFilteredAsync(CustomerFilterRequest request)
        {
            return await _customerRepository
                .GetFilteredAsync(request);
        }

        public async Task<Customer?> PatchAsync(
    int id,
    UpdateCustomerPatchDto request)
        {
            var customer = await _customerRepository
                .GetByIdAsync(id);

            if(customer is null)
            {
                return null;
            }


            if(request.FirstName != null)
            {
                customer.FirstName = request.FirstName;
            }


            if(request.LastName != null)
            {
                customer.LastName = request.LastName;
            }


            if(request.Email != null)
            {
                customer.Email = request.Email;
            }


            if(request.PhoneNumber != null)
            {
                customer.PhoneNumber = request.PhoneNumber;
            }


            if(request.CompanyId.HasValue)
            {
                customer.CompanyId = request.CompanyId.Value;
            }


            if(request.IsActive.HasValue)
            {
                customer.IsActive = request.IsActive.Value;
            }


            await _customerRepository.UpdateAsync(customer);


            return customer;
        }
    }
}
