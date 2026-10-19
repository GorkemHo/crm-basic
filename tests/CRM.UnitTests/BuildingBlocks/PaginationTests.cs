using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Messaging;
using CRM.BuildingBlocks.Pagination;

namespace CRM.UnitTests.BuildingBlocks;

public class PaginationTests
{
    private sealed class Item
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = [];
    }

    private static readonly IQueryable<Item> Items = new[]
    {
        new Item { Id = 1, Name = "Beta" },
        new Item { Id = 2, Name = "Alpha" },
        new Item { Id = 3, Name = "Gamma" }
    }.AsQueryable();

    [Fact]
    public void ApplySorting_IsCaseInsensitive_AndSupportsDescending()
    {
        Assert.Equal(["Gamma", "Beta", "Alpha"], Items.ApplySorting("name", "desc").Select(x => x.Name));
    }

    [Fact]
    public void ApplySorting_NoField_LeavesOrderUnchanged()
    {
        Assert.Equal([1, 2, 3], Items.ApplySorting(null, null).Select(x => x.Id));
    }

    [Theory]
    [InlineData("doesNotExist", "asc", "query.invalid_sort_field")]
    [InlineData("tags", "asc", "query.invalid_sort_field")]
    [InlineData("name", "sideways", "query.invalid_sort_direction")]
    public void ApplySorting_InvalidInput_ThrowsValidation(string sortBy, string direction, string code)
    {
        var exception = Assert.Throws<ValidationException>(() => Items.ApplySorting(sortBy, direction).ToList());

        Assert.Equal(code, exception.ErrorCode);
        Assert.NotNull(exception.Errors);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 500, 1, PaginationRequest.MaxPageSize)]
    [InlineData(3, 25, 3, 25)]
    public void PaginationRequest_ClampsValues(int pageNumber, int pageSize, int expectedPage, int expectedSize)
    {
        var request = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedSize, request.Size);
    }

    [Fact]
    public void PagedResponse_ComputesTotalPages()
    {
        Assert.Equal(3, new PagedResponse<int> { PageSize = 10, TotalCount = 21 }.TotalPages);
    }

    private sealed record CustomerCreated;

    [Fact]
    public void MessageRouting_DerivesDottedLowercaseKey()
    {
        Assert.Equal("customer.created", MessageRouting.For<CustomerCreated>());
    }
}
