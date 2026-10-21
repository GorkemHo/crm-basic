using CRM.BuildingBlocks.Reporting;
using CRM.Modules.Reporting.Features;

namespace CRM.UnitTests.Reporting;

public class ReportPeriodTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    [Fact]
    public void Range_DefaultsToTheLastSixCalendarMonths_UpToTheEndOfToday()
    {
        var (from, to) = new GetSalesReport().Range(Today);

        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc), to);
    }

    [Fact]
    public void Range_IncludesBothEndDays()
    {
        var (from, to) = new GetSalesReport { From = new DateOnly(2026, 1, 15), To = new DateOnly(2026, 1, 15) }.Range(Today);

        Assert.Equal(TimeSpan.FromDays(1), to - from);
        Assert.Equal(DateTimeKind.Utc, from.Kind);
    }

    [Fact]
    public void Months_CoverEveryMonthTouchedByTheRange()
    {
        var months = ReportMonths.Between(new DateTime(2025, 11, 20), new DateTime(2026, 2, 2)).ToList();

        Assert.Equal([(2025, 11), (2025, 12), (2026, 1), (2026, 2)], months);
    }

    [Fact]
    public void Months_EndingExactlyOnAMonthStart_DoNotIncludeThatMonth()
    {
        var months = ReportMonths.Between(new DateTime(2026, 1, 1), new DateTime(2026, 3, 1)).ToList();

        Assert.Equal([(2026, 1), (2026, 2)], months);
    }
}
