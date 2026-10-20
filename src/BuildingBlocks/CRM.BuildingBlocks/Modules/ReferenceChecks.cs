using CRM.BuildingBlocks.Exceptions;

namespace CRM.BuildingBlocks.Modules;

/// <summary>Why a record cannot be deleted: another module still uses it.</summary>
public sealed record ReferenceConflict(string Message, string ErrorCode);

/// <summary>
/// Lets a module veto deleting another module's record that it references (e.g. Sales: a company with deals),
/// without the owning module knowing about it. The owner calls <see cref="ReferenceChecks.EnsureNotReferencedAsync"/>
/// with a resource name it publishes in its Contracts.
/// </summary>
public interface IReferenceCheck
{
    /// <returns>A conflict when <paramref name="resource"/> <paramref name="id"/> is in use, otherwise null.</returns>
    Task<ReferenceConflict?> CheckAsync(string resource, int id, CancellationToken cancellationToken);
}

public static class ReferenceChecks
{
    /// <exception cref="ConflictException">When any module reports the record as in use.</exception>
    public static async Task EnsureNotReferencedAsync(
        this IEnumerable<IReferenceCheck> checks, string resource, int id, CancellationToken cancellationToken)
    {
        foreach(var check in checks)
        {
            if(await check.CheckAsync(resource, id, cancellationToken) is { } conflict)
            {
                throw new ConflictException(conflict.Message, conflict.ErrorCode);
            }
        }
    }
}
