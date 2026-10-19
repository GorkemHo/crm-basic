using System.ComponentModel.DataAnnotations;

namespace CRM.Core.Entities
{
    public class Role
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();
    }
}
