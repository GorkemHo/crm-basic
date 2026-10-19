using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.UserDtos
{
    public class UpdateUserDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
