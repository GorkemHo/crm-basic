namespace CRM.Core.Common
{
    public class CustomerFilterRequest : PaginationRequest
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public int? CompanyId { get; set; }
    }
}
