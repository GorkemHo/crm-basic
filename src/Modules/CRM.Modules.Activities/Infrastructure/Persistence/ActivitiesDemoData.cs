using CRM.BuildingBlocks.Application;
using CRM.Modules.Activities.Domain;
using CRM.Modules.Activities.Features;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Sales.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Activities.Infrastructure.Persistence;

/// <summary>
/// Demo notes, calls, meetings and emails on the Crm demo companies (found by tax number), spread over
/// the last weeks plus one planned meeting. Linked to the company's demo deal with the given title,
/// when there is one. Created through the module's commands.
/// </summary>
internal static class ActivitiesDemoData
{
    /// <summary>Marker: when an activity with this subject exists the demo data is already in place.</summary>
    public const string MarkerSubject = "Filo takip ihtiyaç analizi toplantısı";

    /// <param name="DaysAgo">Negative for a planned (future) activity.</param>
    /// <param name="Contact">Index into the company's customers.</param>
    /// <param name="Deal">Title of the company's deal to link to.</param>
    internal sealed record DemoActivity(
        string TaxNumber, string Type, string Subject, string? Notes, int DaysAgo,
        int? DurationMinutes = null, int? Contact = null, string? Deal = null);

    public static readonly IReadOnlyList<DemoActivity> Activities =
    [
        new("4610000001", ActivityTypes.Meeting, MarkerSubject,
            "Araç sayısı 140, mevcut sistem lisans yenilemesi Mart'ta. Teklif için entegrasyon listesi istendi.",
            12, 60, Contact: 0, Deal: "Filo takip yazılımı lisansı"),
        new("4610000001", ActivityTypes.Email, "Teklif taslağı gönderildi", null, 5, Contact: 0, Deal: "Filo takip yazılımı lisansı"),
        new("4610000001", ActivityTypes.Call, "Depo otomasyonu için ilk görüşme", "Haziran'daki yeni depo açılışı için zaman planı konuşuldu.",
            3, 20, Contact: 1, Deal: "Depo otomasyonu danışmanlığı"),
        new("4610000002", ActivityTypes.Meeting, "Bulut geçişi teknik değerlendirme", "Mimari ekip katıldı; pilot kapsamı netleşti.",
            -2, 90, Contact: 0, Deal: "Bulut altyapı geçişi"),
        new("4610000002", ActivityTypes.Note, "Satın alma süreci", "Onay için CFO imzası gerekiyor, bütçe Q1'de açılıyor.", 8, Deal: "Bulut altyapı geçişi"),
        new("4610000004", ActivityTypes.Call, "Sözleşme imzası teyidi", "Soğuk zincir izleme sözleşmesi imzalandı, kurulum tarihi bekleniyor.",
            1, 10, Contact: 0, Deal: "Soğuk zincir izleme"),
        new("4610000005", ActivityTypes.Meeting, "Şantiye ziyareti", "İki aktif şantiyede saha ekipleriyle panel demosu yapıldı.",
            6, 120, Contact: 3, Deal: "Şantiye yönetim paneli"),
        new("4610000006", ActivityTypes.Note, "Kayıp değerlendirmesi", "Müşteri bütçeyi gelecek yıla erteledi; Eylül'de tekrar aranacak.", 2,
            Deal: "Rezervasyon sistemi yenileme"),
        new("4610000008", ActivityTypes.Email, "Referans listesi paylaşıldı", null, 9, Contact: 1),
        new("4610000012", ActivityTypes.Call, "Sezon öncesi takip", "Kampanya takvimi ve kullanıcı sayısı teyit edildi.", 4, 15, Contact: 1,
            Deal: "Sezonluk kampanya CRM kurulumu"),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<ActivitiesDbContext>();
        var crm = services.GetRequiredService<ICrmDirectory>();
        var sales = services.GetRequiredService<ISalesDirectory>();
        var sender = services.GetRequiredService<ISender>();
        var clock = services.GetRequiredService<TimeProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ActivitiesDemoData));

        if(await db.Activities.AnyAsync(x => x.Subject == MarkerSubject, cancellationToken))
        {
            return;
        }

        // Rounded to the hour so the demo timeline looks like real entries.
        var now = clock.GetUtcNow().UtcDateTime;
        var baseTime = new DateTime(now.Year, now.Month, now.Day, 10, 0, 0, DateTimeKind.Utc);
        var created = 0;

        foreach(var demo in Activities)
        {
            if(await crm.FindCompanyByTaxNumberAsync(demo.TaxNumber, cancellationToken) is not { } company)
            {
                continue; // The Crm demo data isn't there (or was changed); skip rather than fail.
            }

            var contacts = await crm.GetCustomersOfCompanyAsync(company.Id, cancellationToken);
            int? contactId = demo.Contact is { } index && index < contacts.Count ? contacts[index].Id : null;
            int? dealId = demo.Deal is null ? null
                : (await sales.GetDealsOfCompanyAsync(company.Id, cancellationToken)).FirstOrDefault(d => d.Title == demo.Deal)?.Id;

            await sender.Send(new LogActivity(
                demo.Type, demo.Subject, demo.Notes, baseTime.AddDays(-demo.DaysAgo).AddHours(created % 5),
                demo.DurationMinutes, company.Id, contactId, dealId), cancellationToken);
            created++;
        }

        logger.LogInformation("Seeded {Activities} demo activities", created);
    }
}
