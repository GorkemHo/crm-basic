using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.CompanyDtos
{
    public class UpdateCompanyPatchDto
    {
        [MaxLength(100)]
        public string? CompanyName { get; set; }
        [MaxLength(11)]
        public string? TaxNumber { get; set; }

        public bool? IsActive { get; set; }
    }
}
