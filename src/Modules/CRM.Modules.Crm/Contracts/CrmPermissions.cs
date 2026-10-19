using CRM.BuildingBlocks.Security;

namespace CRM.Modules.Crm.Contracts;

/// <summary>Permissions checked by the Crm module.</summary>
public static class CrmPermissions
{
    public const string CompaniesRead = "crm.companies.read";
    public const string CompaniesWrite = "crm.companies.write";
    public const string CompaniesDelete = "crm.companies.delete";
    public const string CustomersRead = "crm.customers.read";
    public const string CustomersWrite = "crm.customers.write";
    public const string CustomersDelete = "crm.customers.delete";

    internal static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(CompaniesRead, "Companies", "View companies", GrantedToUsersByDefault: true),
        new(CompaniesWrite, "Companies", "Create and edit companies", GrantedToUsersByDefault: true),
        new(CompaniesDelete, "Companies", "Delete companies"),
        new(CustomersRead, "Customers", "View customers", GrantedToUsersByDefault: true),
        new(CustomersWrite, "Customers", "Create and edit customers", GrantedToUsersByDefault: true),
        new(CustomersDelete, "Customers", "Delete customers")
    ];
}
