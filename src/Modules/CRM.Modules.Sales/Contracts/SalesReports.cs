namespace CRM.Modules.Sales.Contracts;

/// <summary>Deals in one currency (amounts in different currencies are never added together).</summary>
public sealed record CurrencyAmount(string Currency, int Count, decimal Amount);

/// <summary>Deals closed in one calendar month (UTC).</summary>
public sealed record MonthlySales(int Year, int Month, IReadOnlyList<CurrencyAmount> Won, int LostCount);

public sealed record LostReasonCount(string? Reason, int Count);

/// <param name="WinRate">Won ÷ (won + lost) closed in the period, 0–1; null when nothing closed.</param>
/// <param name="AverageCycleDays">Average days from creating to winning a deal; null when nothing was won.</param>
public sealed record SalesReport(
    IReadOnlyList<MonthlySales> Months,
    IReadOnlyList<CurrencyAmount> Won,
    int WonCount,
    int LostCount,
    double? WinRate,
    double? AverageCycleDays,
    IReadOnlyList<LostReasonCount> LostReasons);

public sealed record StageCurrencyTotal(string Currency, int Count, decimal Amount, decimal WeightedAmount);

/// <summary>Open deals in one stage right now.</summary>
public sealed record PipelineStageReport(int StageId, string Name, int Probability, IReadOnlyList<StageCurrencyTotal> Totals);

/// <summary>Sales figures for the Reporting module.</summary>
public interface ISalesReports
{
    /// <summary>Deals closed in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), by month of closing.</summary>
    Task<SalesReport> GetSalesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    /// <summary>The current open pipeline, in board order.</summary>
    Task<IReadOnlyList<PipelineStageReport>> GetPipelineAsync(CancellationToken cancellationToken);
}
