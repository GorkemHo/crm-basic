using CRM.BuildingBlocks.Domain;

namespace CRM.Modules.Attachments.Domain;

/// <summary>
/// A file attached to a record of another module. The bytes live in the file store under
/// <see cref="StorageKey"/>; the uploader is <see cref="AuditableEntity.CreatedBy"/>.
/// </summary>
internal sealed class Attachment : SoftDeletableEntity
{
    /// <summary>One of <see cref="RecordTypes.All"/>.</summary>
    public string RecordType { get; set; } = string.Empty;

    public int RecordId { get; set; }

    /// <summary>Original name, cleaned of any path; only used for display and downloads.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>From the extension (never from the client).</summary>
    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    /// <summary>Server-generated name in the file store.</summary>
    public string StorageKey { get; set; } = string.Empty;
}

internal static class RecordTypes
{
    public const string Company = "company";
    public const string Customer = "customer";
    public const string Deal = "deal";

    public static readonly string[] All = [Company, Customer, Deal];
}
