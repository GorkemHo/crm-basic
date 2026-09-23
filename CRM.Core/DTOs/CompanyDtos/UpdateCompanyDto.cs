using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.CompanyDtos
{
    public class UpdateCompanyDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(11)]
        public string? TaxNumber { get; set; }

        public bool IsActive { get; set; }
    }
}
