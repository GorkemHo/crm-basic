using CRM.BuildingBlocks.Domain;

namespace CRM.Modules.Crm.Domain;

internal sealed class Company : AuditableEntity
{
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Optional; unique when present.</summary>
    public string? TaxNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

internal sealed class Customer : AuditableEntity
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;
}
