using CRM.BuildingBlocks.Domain;

namespace CRM.Modules.Sales.Domain;

/// <summary>A step of the sales pipeline. Admins manage the list; open deals sit in one stage.</summary>
internal sealed class PipelineStage : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Order on the board, 0-based.</summary>
    public int Position { get; set; }

    /// <summary>Chance (0–100) that a deal in this stage is won; used for the weighted pipeline value.</summary>
    public int Probability { get; set; }
}

internal sealed class Deal : SoftDeletableEntity
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Company in the Crm module (referenced by id, no cross-schema foreign key).</summary>
    public int CompanyId { get; set; }

    /// <summary>Optional contact person: a customer of <see cref="CompanyId"/>.</summary>
    public int? CustomerId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>ISO 4217 code, one of <see cref="Currencies.All"/>.</summary>
    public string Currency { get; set; } = Currencies.Try;

    public int StageId { get; set; }

    public PipelineStage Stage { get; set; } = null!;

    /// <summary>One of <see cref="DealStatus"/>.</summary>
    public string Status { get; set; } = DealStatus.Open;

    public DateOnly? ExpectedCloseDate { get; set; }

    /// <summary>When the deal was won or lost.</summary>
    public DateTime? ClosedAt { get; set; }

    public string? LostReason { get; set; }
}

internal static class DealStatus
{
    public const string Open = "open";
    public const string Won = "won";
    public const string Lost = "lost";

    public static readonly string[] All = [Open, Won, Lost];
}

internal static class Currencies
{
    public const string Try = "TRY";
    public const string Usd = "USD";
    public const string Eur = "EUR";

    public static readonly string[] All = [Try, Usd, Eur];
}
