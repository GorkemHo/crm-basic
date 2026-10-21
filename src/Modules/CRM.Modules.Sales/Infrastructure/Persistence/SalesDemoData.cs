using CRM.BuildingBlocks.Application;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Sales.Domain;
using CRM.Modules.Sales.Features.Deals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Sales.Infrastructure.Persistence;

/// <summary>
/// Demo deals for the Crm module's demo companies (found by tax number through <see cref="ICrmDirectory"/>),
/// spread over the pipeline in all three currencies, with some won and lost this month.
/// Created through the module's commands.
/// </summary>
internal static class SalesDemoData
{
    /// <summary>Marker: when a deal with this title exists the demo data is already in place.</summary>
    public const string MarkerTitle = "Filo takip yazılımı lisansı";

    /// <param name="Stage">Index into the pipeline (clamped to the last stage).</param>
    /// <param name="Contact">Index into the company's customers, if any.</param>
    /// <param name="CloseInDays">Expected close date relative to today.</param>
    /// <param name="ClosedMonthsAgo">For history: the deal was closed this many months ago, <paramref name="CycleDays"/> after it was added.</param>
    internal sealed record DemoDeal(
        string TaxNumber, string Title, decimal Amount, string Currency, int Stage,
        string Status = DealStatus.Open, int? CloseInDays = null, int? Contact = null, string? LostReason = null,
        int? ClosedMonthsAgo = null, int CycleDays = 30);

    public static readonly IReadOnlyList<DemoDeal> Deals =
    [
        new("4610000001", MarkerTitle, 450_000m, Currencies.Try, 2, CloseInDays: 20, Contact: 0),
        new("4610000001", "Depo otomasyonu danışmanlığı", 85_000m, Currencies.Usd, 1, CloseInDays: 45),
        new("4610000002", "Bulut altyapı geçişi", 120_000m, Currencies.Usd, 3, CloseInDays: 10, Contact: 0),
        new("4610000003", "ERP entegrasyonu", 60_000m, Currencies.Eur, 0, CloseInDays: 60, Contact: 1),
        new("4610000004", "Soğuk zincir izleme", 275_000m, Currencies.Try, 3, DealStatus.Won, Contact: 0),
        new("4610000005", "Şantiye yönetim paneli", 1_250_000m, Currencies.Try, 2, CloseInDays: 30, Contact: 3),
        new("4610000006", "Rezervasyon sistemi yenileme", 40_000m, Currencies.Eur, 2, DealStatus.Lost, Contact: 1,
            LostReason: "Bütçe bir sonraki yıla ertelendi."),
        new("4610000008", "Saha bakım mobil uygulaması", 95_000m, Currencies.Usd, 1, CloseInDays: 25),
        new("4610000009", "Bayi portalı", 180_000m, Currencies.Try, 3, DealStatus.Won, Contact: 0),
        new("4610000010", "Stok tahminleme modülü", 30_000m, Currencies.Eur, 0),
        new("4610000012", "Sezonluk kampanya CRM kurulumu", 22_000m, Currencies.Eur, 3, CloseInDays: 5, Contact: 1),

        // History for the reports: deals closed over the last months.
        new("4610000002", "Mobil uygulama bakım sözleşmesi", 48_000m, Currencies.Usd, 3, DealStatus.Won, ClosedMonthsAgo: 5, CycleDays: 40),
        new("4610000003", "Kumaş stok entegrasyonu", 22_000m, Currencies.Eur, 2, DealStatus.Lost, LostReason: "Fiyat yüksek bulundu.", ClosedMonthsAgo: 5),
        new("4610000004", "Bayi sipariş portalı", 310_000m, Currencies.Try, 3, DealStatus.Won, ClosedMonthsAgo: 4, CycleDays: 55),
        new("4610000006", "Otel kanal yöneticisi", 35_000m, Currencies.Eur, 3, DealStatus.Won, ClosedMonthsAgo: 4, CycleDays: 25),
        new("4610000008", "Enerji izleme panosu", 60_000m, Currencies.Usd, 2, DealStatus.Lost, LostReason: "Rakip firma tercih edildi.", ClosedMonthsAgo: 3),
        new("4610000009", "Tarla sensör pilotu", 140_000m, Currencies.Try, 3, DealStatus.Won, ClosedMonthsAgo: 3, CycleDays: 70),
        new("4610000010", "Yedek parça e-ticaret sitesi", 520_000m, Currencies.Try, 3, DealStatus.Won, ClosedMonthsAgo: 2, CycleDays: 45),
        new("4610000012", "Kayak okulu rezervasyonu", 18_000m, Currencies.Eur, 1, DealStatus.Lost, LostReason: "Bütçe bir sonraki yıla ertelendi.", ClosedMonthsAgo: 2),
        new("4610000005", "İş güvenliği eğitim modülü", 95_000m, Currencies.Try, 3, DealStatus.Won, ClosedMonthsAgo: 1, CycleDays: 20),
        new("4610000001", "Rota optimizasyonu danışmanlığı", 40_000m, Currencies.Usd, 3, DealStatus.Won, ClosedMonthsAgo: 1, CycleDays: 35),
        new("4610000002", "Veri ambarı ön çalışması", 15_000m, Currencies.Usd, 1, DealStatus.Lost, LostReason: "Fiyat yüksek bulundu.", ClosedMonthsAgo: 1),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<SalesDbContext>();
        var directory = services.GetRequiredService<ICrmDirectory>();
        var sender = services.GetRequiredService<ISender>();
        var clock = services.GetRequiredService<TimeProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SalesDemoData));

        if(await db.Deals.AnyAsync(x => x.Title == MarkerTitle, cancellationToken))
        {
            return;
        }

        var stageIds = await db.Stages.OrderBy(x => x.Position).Select(x => x.Id).ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var created = 0;

        foreach(var demo in Deals)
        {
            if(await directory.FindCompanyByTaxNumberAsync(demo.TaxNumber, cancellationToken) is not { IsActive: true } company)
            {
                continue; // The Crm demo data isn't there (or was changed); skip rather than fail.
            }

            var contacts = await directory.GetCustomersOfCompanyAsync(company.Id, cancellationToken);
            int? contactId = demo.Contact is { } index && index < contacts.Count ? contacts[index].Id : null;

            var deal = await sender.Send(new CreateDeal(
                demo.Title, company.Id, contactId, demo.Amount, demo.Currency,
                stageIds[Math.Min(demo.Stage, stageIds.Count - 1)],
                demo.CloseInDays is { } days ? today.AddDays(days) : null), cancellationToken);

            if(demo.Status == DealStatus.Won) await sender.Send(new WinDeal(deal.Id), cancellationToken);
            if(demo.Status == DealStatus.Lost) await sender.Send(new LoseDeal(deal.Id, demo.LostReason), cancellationToken);

            if(demo.ClosedMonthsAgo is { } monthsAgo)
            {
                // Commands always use "now"; history is moved back afterwards (demo data only).
                // Day 2–11 of the target month, so a deal never slips into the month before.
                var month = clock.GetUtcNow().UtcDateTime.AddMonths(-monthsAgo);
                var closedAt = new DateTime(month.Year, month.Month, 2 + created % 10, 10, 0, 0, DateTimeKind.Utc);
                var createdAt = closedAt.AddDays(-demo.CycleDays);
                await db.Deals.Where(x => x.Id == deal.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ClosedAt, closedAt)
                    .SetProperty(x => x.CreatedAt, createdAt)
                    .SetProperty(x => x.UpdatedAt, closedAt), cancellationToken);
            }

            created++;
        }

        logger.LogInformation("Seeded {Deals} demo deals", created);
    }
}
