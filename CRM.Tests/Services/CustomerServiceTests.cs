using System.Linq.Expressions;
using CRM.Core.DTOs.CustomerDtos;
using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Services;
using NSubstitute;

namespace CRM.Tests.Services;

public class CustomerServiceTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly ICompanyRepository _companies = Substitute.For<ICompanyRepository>();
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _sut = new CustomerService(_customers, _companies);
        _customers.AddAsync(Arg.Any<Customer>()).Returns(call => call.Arg<Customer>());
    }

    private void CompanyExists(bool exists) =>
        _companies.ExistsAsync(Arg.Any<Expression<Func<Company, bool>>>()).Returns(exists);

    [Fact]
    public async Task Create_UnknownCompany_ThrowsNotFound()
    {
        CompanyExists(false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.CreateAsync(new Customer { Email = "a@b.c", CompanyId = 99 }));
    }

    [Fact]
    public async Task Create_DuplicateEmail_Throws()
    {
        CompanyExists(true);
        _customers.GetByEmailAsync("a@b.c").Returns(new Customer { Id = 1, Email = "a@b.c" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.CreateAsync(new Customer { Email = "a@b.c", CompanyId = 1 }));
    }

    [Fact]
    public async Task Patch_OnlyChangesProvidedFields_AndSetsUpdatedAt()
    {
        var customer = new Customer
        {
            Id = 3,
            FirstName = "Grace",
            LastName = "Hopper",
            Email = "grace@example.com",
            CompanyId = 1,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _customers.GetByIdAsync(3).Returns(customer);

        await _sut.PatchAsync(3, new UpdateCustomerPatchDto { IsActive = false });

        Assert.Equal("Grace", customer.FirstName);
        Assert.Equal("grace@example.com", customer.Email);
        Assert.False(customer.IsActive);
        Assert.True(customer.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Patch_MoveToUnknownCompany_ThrowsNotFound()
    {
        _customers.GetByIdAsync(3).Returns(new Customer { Id = 3, CompanyId = 1 });
        CompanyExists(false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.PatchAsync(3, new UpdateCustomerPatchDto { CompanyId = 99 }));
    }
}
