namespace CRM.Core.DTOs.CompanyDtos
{
    public class CompanyDto
    {
        public int Id { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public string? TaxNumber { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
