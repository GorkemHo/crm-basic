using CRM.BuildingBlocks.Domain;

namespace CRM.Modules.Activities.Domain;

/// <summary>
/// Something that happened with a customer: a note, call, meeting or email.
/// Always about a company; optionally also a contact person and a deal of that company.
/// The author is <see cref="AuditableEntity.CreatedBy"/>.
/// </summary>
internal sealed class Activity : SoftDeletableEntity
{
    /// <summary>One of <see cref="ActivityTypes.All"/>.</summary>
    public string Type { get; set; } = ActivityTypes.Note;

    public string Subject { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>When it happened (UTC); may be in the past (logged later) or future (planned meeting).</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>For calls and meetings.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>Crm company (referenced by id, no cross-schema foreign key).</summary>
    public int CompanyId { get; set; }

    /// <summary>Crm customer of <see cref="CompanyId"/>.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Sales deal of <see cref="CompanyId"/>.</summary>
    public int? DealId { get; set; }
}

internal static class ActivityTypes
{
    public const string Note = "note";
    public const string Call = "call";
    public const string Meeting = "meeting";
    public const string Email = "email";

    public static readonly string[] All = [Note, Call, Meeting, Email];
}
