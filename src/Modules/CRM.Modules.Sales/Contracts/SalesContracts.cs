using CRM.BuildingBlocks.Messaging;
using CRM.BuildingBlocks.Security;

namespace CRM.Modules.Sales.Contracts;

/// <summary>Permissions checked by the Sales module.</summary>
public static class SalesPermissions
{
    public const string DealsRead = "sales.deals.read";
    public const string DealsWrite = "sales.deals.write";
    public const string DealsDelete = "sales.deals.delete";
    public const string StagesManage = "sales.stages.manage";

    internal static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(DealsRead, "Deals", "View deals and the pipeline", GrantedToUsersByDefault: true),
        new(DealsWrite, "Deals", "Create, edit, move and close deals", GrantedToUsersByDefault: true),
        new(DealsDelete, "Deals", "Delete deals"),
        new(StagesManage, "Pipeline stages", "Add, rename, reorder and delete pipeline stages")
    ];
}

public sealed record DealCreated(int DealId, int CompanyId, decimal Amount, string Currency) : IntegrationEvent;

public sealed record DealWon(int DealId, int CompanyId, decimal Amount, string Currency) : IntegrationEvent;

public sealed record DealLost(int DealId, int CompanyId, string? Reason) : IntegrationEvent;
