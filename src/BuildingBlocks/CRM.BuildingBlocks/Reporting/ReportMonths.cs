namespace CRM.BuildingBlocks.Reporting;

public static class ReportMonths
{
    /// <summary>Every calendar month that overlaps [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), so charts show empty months too.</summary>
    public static IEnumerable<(int Year, int Month)> Between(DateTime fromUtc, DateTime toUtc)
    {
        for(var month = new DateTime(fromUtc.Year, fromUtc.Month, 1); month < toUtc; month = month.AddMonths(1))
        {
            yield return (month.Year, month.Month);
        }
    }
}
