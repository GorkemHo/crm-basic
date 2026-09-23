using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs.RoleDtos
{
    public class CreateRoleDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
    }
}
