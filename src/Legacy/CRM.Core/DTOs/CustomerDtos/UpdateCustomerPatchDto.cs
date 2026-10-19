using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.CustomerDtos
{
    public class UpdateCustomerPatchDto
    {
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public int? CompanyId { get; set; }

        public bool? IsActive { get; set; }
    }
}
