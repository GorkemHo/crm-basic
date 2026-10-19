using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.CustomerDtos
{
    public class CreateCustomerDto
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        public int CompanyId { get; set; }
    }
}
