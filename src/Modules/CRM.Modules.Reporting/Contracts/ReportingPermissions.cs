using CRM.BuildingBlocks.Security;

namespace CRM.Modules.Reporting.Contracts;

/// <summary>Permissions checked by the Reporting module.</summary>
public static class ReportingPermissions
{
    /// <summary>Team-wide figures (everyone's deals, activities and tasks), so not granted to users by default.</summary>
    public const string Read = "reporting.reports.read";

    internal static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Read, "Reports", "View sales, pipeline, activity and task reports for the whole team")
    ];
}
