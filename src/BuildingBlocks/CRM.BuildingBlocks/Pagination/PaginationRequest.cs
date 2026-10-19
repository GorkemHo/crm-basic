namespace CRM.BuildingBlocks.Pagination;

/// <summary>Query-string paging and sorting parameters. Values are clamped to safe ranges.</summary>
public class PaginationRequest
{
    public const int MaxPageSize = 100;

    private int _pageNumber = 1;
    public int PageNumber { get => _pageNumber; set => _pageNumber = Math.Max(1, value); }

    private int _pageSize = 10;
    public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, MaxPageSize); }

    /// <summary>Property name to sort by (case-insensitive). Only scalar properties are allowed.</summary>
    public string? SortBy { get; set; }

    /// <summary><c>asc</c> (default) or <c>desc</c>.</summary>
    public string? SortDirection { get; set; } = "asc";
}

public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
