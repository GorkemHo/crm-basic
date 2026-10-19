namespace CRM.BuildingBlocks.Pagination;

/// <summary>
/// Query-string paging and sorting parameters. Inputs are optional (so minimal API binding doesn't
/// require them); use <see cref="Page"/> and <see cref="Size"/>, which are clamped to safe ranges.
/// </summary>
public class PaginationRequest
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    /// <summary>1-based page number (default 1).</summary>
    public int? PageNumber { get; set; }

    /// <summary>Items per page (default 10, max 100).</summary>
    public int? PageSize { get; set; }

    /// <summary>Property name to sort by (case-insensitive). Only scalar properties are allowed.</summary>
    public string? SortBy { get; set; }

    /// <summary><c>asc</c> (default) or <c>desc</c>.</summary>
    public string? SortDirection { get; set; }

    public int Page => Math.Max(1, PageNumber ?? 1);

    public int Size => Math.Clamp(PageSize ?? DefaultPageSize, 1, MaxPageSize);
}

public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
