using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.UserDtos
{
    public class UpdateUserPatchDto
    {
        public int? Id { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? LastName { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(150)]
        public string? Email { get; set; } = string.Empty;

        public bool? IsActive { get; set; }
    }
}
