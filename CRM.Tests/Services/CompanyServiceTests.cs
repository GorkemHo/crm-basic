using System.Linq.Expressions;
using CRM.Core.DTOs.CompanyDtos;
using CRM.Core.Entities;
using CRM.Core.Exceptions;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Services;
using NSubstitute;

namespace CRM.Tests.Services;

public class CompanyServiceTests
{
    private readonly ICompanyRepository _companies = Substitute.For<ICompanyRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly CompanyService _sut;

    public CompanyServiceTests()
    {
        _sut = new CompanyService(_companies, _customers);
        _companies.AddAsync(Arg.Any<Company>()).Returns(call => call.Arg<Company>());
    }

    [Fact]
    public async Task Create_WithoutTaxNumber_SkipsUniquenessCheck()
    {
        await _sut.CreateAsync(new Company { CompanyName = "No Tax Ltd" });

        await _companies.DidNotReceive().GetByTaxNumberAsync(Arg.Any<string>());
        await _companies.Received(1).AddAsync(Arg.Any<Company>());
    }

    [Fact]
    public async Task Create_DuplicateTaxNumber_Throws()
    {
        _companies.GetByTaxNumberAsync("123").Returns(new Company { Id = 1, TaxNumber = "123" });

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(new Company { CompanyName = "Copy", TaxNumber = "123" }));
    }

    [Fact]
    public async Task Patch_KeepingOwnTaxNumber_IsAllowed_AndSetsUpdatedAt()
    {
        var company = new Company { Id = 5, CompanyName = "Acme", TaxNumber = "123", UpdatedAt = DateTime.UtcNow.AddDays(-1) };
        _companies.GetByIdAsync(5).Returns(company);
        _companies.GetByTaxNumberAsync("123").Returns(company);

        var result = await _sut.PatchAsync(5, new UpdateCompanyPatchDto { TaxNumber = "123" });

        Assert.NotNull(result);
        Assert.True(company.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Patch_TaxNumberOfAnotherCompany_Throws()
    {
        _companies.GetByIdAsync(5).Returns(new Company { Id = 5, CompanyName = "Acme" });
        _companies.GetByTaxNumberAsync("999").Returns(new Company { Id = 6, TaxNumber = "999" });

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.PatchAsync(5, new UpdateCompanyPatchDto { TaxNumber = "999" }));
    }

    [Fact]
    public async Task Delete_CompanyWithCustomers_Throws()
    {
        _companies.GetByIdAsync(5).Returns(new Company { Id = 5 });
        _customers.ExistsAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteAsync(5));
        await _companies.DidNotReceive().DeleteAsync(Arg.Any<Company>());
    }

    [Fact]
    public async Task Delete_Missing_ReturnsFalse()
    {
        _companies.GetByIdAsync(5).Returns((Company?)null);

        Assert.False(await _sut.DeleteAsync(5));
    }
}
