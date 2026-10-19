namespace CRM.Core.Common
{
    public class CompanyFilterRequest : PaginationRequest
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
    }
}
