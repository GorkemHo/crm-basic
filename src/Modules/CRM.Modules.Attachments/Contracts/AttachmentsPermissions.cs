using CRM.BuildingBlocks.Security;

namespace CRM.Modules.Attachments.Contracts;

/// <summary>Permissions checked by the Attachments module.</summary>
public static class AttachmentsPermissions
{
    public const string Read = "attachments.files.read";

    /// <summary>Upload files, and delete the ones you uploaded.</summary>
    public const string Write = "attachments.files.write";

    /// <summary>Delete anyone's files.</summary>
    public const string Manage = "attachments.files.manage";

    internal static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Read, "Files", "View and download files", GrantedToUsersByDefault: true),
        new(Write, "Files", "Upload files; delete your own", GrantedToUsersByDefault: true),
        new(Manage, "Files", "Delete anyone's files")
    ];
}
