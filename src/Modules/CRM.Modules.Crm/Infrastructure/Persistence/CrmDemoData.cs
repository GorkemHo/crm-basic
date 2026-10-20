using CRM.BuildingBlocks.Application;
using CRM.Modules.Crm.Features.Companies;
using CRM.Modules.Crm.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Crm.Infrastructure.Persistence;

/// <summary>
/// Demo companies and their contacts, created through the module's commands (so validation, uniqueness
/// rules, audit fields and integration events all apply). Fictional names; emails use the reserved
/// <c>.test</c> domain.
/// </summary>
internal static class CrmDemoData
{
    /// <summary>Marker: when a company with this tax number exists the demo data is already in place.</summary>
    public const string MarkerTaxNumber = "4610000001";

    internal sealed record DemoCustomer(string FirstName, string LastName, string Phone, bool Active = true);

    internal sealed record DemoCompany(string Name, string? TaxNumber, string Domain, DemoCustomer[] Customers, bool Active = true);

    public static readonly IReadOnlyList<DemoCompany> Companies =
    [
        new("Anadolu Lojistik A.Ş.", MarkerTaxNumber, "anadolu-lojistik.test",
        [
            new("Hakan", "Arslan", "+90 532 410 1101"),
            new("Zeynep", "Koç", "+90 533 410 1102"),
            new("Burak", "Şahin", "+90 535 410 1103"),
        ]),
        new("Boğaziçi Yazılım Ltd. Şti.", "4610000002", "bogazici-yazilim.test",
        [
            new("Selin", "Aydın", "+90 532 410 2201"),
            new("Emre", "Çelik", "+90 541 410 2202"),
        ]),
        new("Ege Tekstil San. ve Tic. A.Ş.", "4610000003", "ege-tekstil.test",
        [
            new("Deniz", "Yıldız", "+90 532 410 3301"),
            new("Gökhan", "Aksoy", "+90 542 410 3302"),
            new("Merve", "Kurt", "+90 543 410 3303", Active: false),
        ]),
        new("Karadeniz Gıda Pazarlama", "4610000004", "karadeniz-gida.test",
        [
            new("Oğuz", "Polat", "+90 536 410 4401"),
            new("Ebru", "Taş", "+90 537 410 4402"),
        ]),
        new("Marmara Yapı İnşaat A.Ş.", "4610000005", "marmara-yapi.test",
        [
            new("Serkan", "Özdemir", "+90 532 410 5501"),
            new("Pınar", "Erdoğan", "+90 533 410 5502"),
            new("Kemal", "Bulut", "+90 534 410 5503"),
            new("İrem", "Güneş", "+90 535 410 5504"),
        ]),
        new("Akdeniz Turizm ve Otelcilik", "4610000006", "akdeniz-turizm.test",
        [
            new("Caner", "Yalçın", "+90 532 410 6601"),
            new("Ceren", "Kılıç", "+90 538 410 6602"),
        ]),
        new("Kapadokya Seramik Atölyesi", null, "kapadokya-seramik.test",
        [
            new("Yusuf", "Doğan", "+90 539 410 7701"),
        ]),
        new("Toros Enerji Sistemleri A.Ş.", "4610000008", "toros-enerji.test",
        [
            new("Tolga", "Kaplan", "+90 532 410 8801"),
            new("Aslı", "Turan", "+90 544 410 8802"),
        ]),
        new("Fırat Tarım Ürünleri", "4610000009", "firat-tarim.test",
        [
            new("Murat", "Ateş", "+90 545 410 9901"),
            new("Esra", "Karaca", "+90 546 410 9902", Active: false),
        ]),
        new("Trakya Otomotiv Yedek Parça", "4610000010", "trakya-otomotiv.test",
        [
            new("Volkan", "Çakır", "+90 532 411 0001"),
            new("Gizem", "Avcı", "+90 547 411 0002"),
        ]),
        new("Nemrut Medya Ajansı", "4610000011", "nemrut-medya.test",
        [
            new("Barış", "Sezer", "+90 548 411 1101"),
        ], Active: false),
        new("Sarıkamış Kış Sporları Ltd.", "4610000012", "sarikamis-kis.test",
        [
            new("Onur", "Kara", "+90 549 411 2201"),
            new("Damla", "Aslan", "+90 532 411 2202"),
        ]),
    ];

    public static int CustomerCount => Companies.Sum(c => c.Customers.Length);

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<CrmDbContext>();
        var sender = services.GetRequiredService<ISender>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CrmDemoData));

        if(await db.Companies.AnyAsync(x => x.TaxNumber == MarkerTaxNumber, cancellationToken))
        {
            return;
        }

        foreach(var demo in Companies)
        {
            var company = await sender.Send(new CreateCompany(demo.Name, demo.TaxNumber), cancellationToken);

            foreach(var contact in demo.Customers)
            {
                var email = $"{Ascii(contact.FirstName)}.{Ascii(contact.LastName)}@{demo.Domain}";
                var customer = await sender.Send(
                    new CreateCustomer(contact.FirstName, contact.LastName, email, contact.Phone, company.Id),
                    cancellationToken);

                if(!contact.Active)
                {
                    await sender.Send(new UpdateCustomer(customer.Id, null, null, null, null, null, IsActive: false), cancellationToken);
                }
            }

            if(!demo.Active)
            {
                await sender.Send(new UpdateCompany(company.Id, null, null, IsActive: false), cancellationToken);
            }
        }

        logger.LogInformation("Seeded {Companies} demo companies and {Customers} customers", Companies.Count, CustomerCount);
    }

    /// <summary>Lowercase ASCII for email local parts ("Şahin" → "sahin").</summary>
    private static string Ascii(string value) => value
        .Replace('İ', 'i') // ToLowerInvariant leaves the Turkish dotted capital I unchanged
        .ToLowerInvariant()
        .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i')
        .Replace('ö', 'o').Replace('ş', 's').Replace('ü', 'u');
}
