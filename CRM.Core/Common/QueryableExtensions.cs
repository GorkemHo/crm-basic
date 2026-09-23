using System.Linq.Expressions;

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

        var parameter = Expression.Parameter(typeof(T), "x");

        var property = Expression.PropertyOrField(
            parameter,
            sortBy);

        var lambda = Expression.Lambda(property, parameter);

        var methodName =
            sortDirection?.ToLower() == "desc"
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
}