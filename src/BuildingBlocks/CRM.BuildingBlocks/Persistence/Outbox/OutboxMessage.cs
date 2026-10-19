namespace CRM.BuildingBlocks.Persistence.Outbox;

/// <summary>An integration event waiting to be delivered, stored in the module's own schema.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary><c>Namespace.TypeName, AssemblyName</c> of the event.</summary>
    public string Type { get; set; } = string.Empty;

    public string Content { get; set; } = "{}";

    public DateTime OccurredAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? Error { get; set; }
}
