using CRM.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Sales.Infrastructure.Persistence;

/// <summary>
/// The starting pipeline, created once when the module finds no stages (every deployment needs one).
/// Admins rename, reorder and extend it afterwards.
/// </summary>
internal static class DefaultStages
{
    public static readonly IReadOnlyList<(string Name, int Probability)> All =
    [
        ("Potansiyel", 10),
        ("Nitelikli", 25),
        ("Teklif", 50),
        ("Pazarlık", 75)
    ];

    public static async Task EnsureAsync(SalesDbContext db, CancellationToken cancellationToken)
    {
        // Soft-deleted stages count too: an admin who removed them all on purpose isn't overridden.
        if(await db.Stages.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        db.Stages.AddRange(All.Select((stage, index) =>
            new PipelineStage { Name = stage.Name, Probability = stage.Probability, Position = index }));
        await db.SaveChangesAsync(cancellationToken);
    }
}
