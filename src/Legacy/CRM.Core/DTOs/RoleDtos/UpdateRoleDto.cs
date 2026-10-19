using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.RoleDtos
{
    public class UpdateRoleDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
