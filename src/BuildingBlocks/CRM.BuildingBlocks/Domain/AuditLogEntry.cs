namespace CRM.BuildingBlocks.Domain;

/// <summary>
/// One change to an auditable entity: who changed which fields from what to what.
/// Stored in each module's own schema (<c>audit_logs</c>), written in the same transaction as the change.
/// </summary>
public sealed class AuditLogEntry
{
    public long Id { get; set; }

    /// <summary>CLR type name, e.g. <c>Company</c>.</summary>
    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    /// <summary><see cref="AuditAction"/> as text.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON object: <c>{ "field": { "old": …, "new": … } }</c>.</summary>
    public string Changes { get; set; } = "{}";

    public int? UserId { get; set; }

    public DateTime OccurredAt { get; set; }
}

public static class AuditAction
{
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
}

/// <summary>Excludes a property from the audit log (secrets, large blobs...).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;
