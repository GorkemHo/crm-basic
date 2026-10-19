using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.CompanyDtos
{
    public class CreateCompanyDto
    {
        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(11)]
        public string? TaxNumber { get; set; }
    }
}
