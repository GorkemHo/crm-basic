using System.ComponentModel.DataAnnotations;

namespace CRM.Infrastructure.Persistence.Entities;

public class Company
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(11)]
    public string? TaxNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
