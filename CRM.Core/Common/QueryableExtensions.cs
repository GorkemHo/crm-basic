using System.Linq.Expressions;
using System.Reflection;

namespace CRM.Core.Common;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplySorting<T>(
        this IQueryable<T> query,
        string? sortBy,
        string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return query;
        }

        // Only public scalar properties of the entity can be sorted on; anything else is a client error.
        var propertyInfo = typeof(T).GetProperty(
            sortBy,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (propertyInfo is null || !IsSortableType(propertyInfo.PropertyType))
        {
            throw new InvalidOperationException($"Invalid sort field: '{sortBy}'.");
        }

        var isDescending = sortDirection?.ToLowerInvariant() switch
        {
            null or "" or "asc" => false,
            "desc" => true,
            _ => throw new InvalidOperationException($"Invalid sort direction: '{sortDirection}'. Use 'asc' or 'desc'.")
        };

        var parameter = Expression.Parameter(typeof(T), "x");

        var property = Expression.Property(parameter, propertyInfo);

        var lambda = Expression.Lambda(property, parameter);

        var methodName = isDescending
            ? "OrderByDescending"
            : "OrderBy";

        var expression = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(T), property.Type },
            query.Expression,
            Expression.Quote(lambda));

        return query.Provider.CreateQuery<T>(expression);
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
