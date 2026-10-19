using CRM.Core.Common;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;
using CRM.Core.Exceptions;
using CRM.Core.Mappings;
using CRM.Core.Messaging;

namespace CRM.Tests.Common;

public class QueryAndMappingTests
{
    private static readonly IQueryable<Company> Companies = new[]
    {
        new Company { Id = 1, CompanyName = "Beta" },
        new Company { Id = 2, CompanyName = "Alpha" },
        new Company { Id = 3, CompanyName = "Gamma" }
    }.AsQueryable();

    [Fact]
    public void ApplySorting_IsCaseInsensitive_AndSupportsDescending()
    {
        var names = Companies.ApplySorting("companyname", "desc").Select(x => x.CompanyName).ToList();

        Assert.Equal(["Gamma", "Beta", "Alpha"], names);
    }

    [Fact]
    public void ApplySorting_NoField_LeavesOrderUnchanged()
    {
        Assert.Equal([1, 2, 3], Companies.ApplySorting(null, null).Select(x => x.Id));
    }

    [Theory]
    [InlineData("doesNotExist", "asc")]
    [InlineData("companyName", "sideways")]
    public void ApplySorting_InvalidInput_ThrowsValidation(string sortBy, string direction)
    {
        Assert.Throws<ValidationException>(() => Companies.ApplySorting(sortBy, direction).ToList());
    }

    [Fact]
    public void ApplySorting_NavigationProperty_IsRejected()
    {
        var users = Array.Empty<User>().AsQueryable();

        Assert.Throws<ValidationException>(() => users.ApplySorting(nameof(User.UserRoles), "asc"));
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 500, 1, 100)]
    [InlineData(3, 25, 3, 25)]
    public void PaginationRequest_ClampsValues(int pageNumber, int pageSize, int expectedPage, int expectedSize)
    {
        var request = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };

        Assert.Equal(expectedPage, request.PageNumber);
        Assert.Equal(expectedSize, request.PageSize);
    }

    [Fact]
    public void UserPatch_OmittedFields_AreNotBlanked()
    {
        // Regression: the patch DTO used to default strings to "", wiping fields that weren't sent.
        var user = new User { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", IsActive = true };

        new UpdateUserPatchDto { IsActive = false }.MapPatchToEntity(user);

        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.Equal("ada@example.com", user.Email);
        Assert.False(user.IsActive);
    }

    private sealed record CustomerCreated;

    [Fact]
    public void MessageRouting_DerivesDottedLowercaseKey()
    {
        Assert.Equal("customer.created", MessageRouting.For<CustomerCreated>());
    }
}
