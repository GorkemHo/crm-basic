namespace CRM.Core.Common
{
    public class PaginationRequest
    {
        private const int MaxPageSize = 100;

        private int _pageNumber = 1;
        public int PageNumber { get => _pageNumber; set => _pageNumber = Math.Max(1, value); }

        private int _pageSize = 10;
        public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, MaxPageSize); }

        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }
}
