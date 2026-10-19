using System.Linq.Expressions;
using System.Reflection;
using CRM.BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CRM.BuildingBlocks.Pagination;

public static class QueryableExtensions
{
    /// <summary>
    /// Sorts by a property name supplied by the client. Unknown or non-scalar properties
    /// raise a <see cref="ValidationException"/> instead of failing deep inside the query.
    /// </summary>
    public static IQueryable<T> ApplySorting<T>(
        this IQueryable<T> query,
        string? sortBy,
        string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return query;
        }

        var propertyInfo = typeof(T).GetProperty(
            sortBy,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (propertyInfo is null || !IsSortableType(propertyInfo.PropertyType))
        {
            throw new ValidationException("sortBy", $"Invalid sort field: '{sortBy}'.", "query.invalid_sort_field");
        }

        var isDescending = sortDirection?.ToLowerInvariant() switch
        {
            null or "" or "asc" => false,
            "desc" => true,
            _ => throw new ValidationException("sortDirection", $"Invalid sort direction: '{sortDirection}'. Use 'asc' or 'desc'.", "query.invalid_sort_direction")
        };

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyInfo);
        var lambda = Expression.Lambda(property, parameter);

        var expression = Expression.Call(
            typeof(Queryable),
            isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy),
            [typeof(T), property.Type],
            query.Expression,
            Expression.Quote(lambda));

        return query.Provider.CreateQuery<T>(expression);
    }

    /// <summary>Applies sorting and paging from <paramref name="request"/> and returns one page plus the total count.</summary>
    public static async Task<PagedResponse<TResult>> ToPagedResponseAsync<T, TResult>(
        this IQueryable<T> query,
        PaginationRequest request,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySorting(request.SortBy, request.SortDirection)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .Select(selector)
            .ToListAsync(cancellationToken);

        return new PagedResponse<TResult>
        {
            Items = items,
            PageNumber = request.Page,
            PageSize = request.Size,
            TotalCount = totalCount
        };
    }

    private static bool IsSortableType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(Guid);
    }
}
