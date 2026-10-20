using System.Text;
using CRM.BuildingBlocks.Application;
using CRM.Modules.Attachments.Domain;
using CRM.Modules.Attachments.Features;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Sales.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Attachments.Infrastructure.Persistence;

/// <summary>
/// Small generated text and CSV files on demo companies and deals (found through the Crm and Sales directories),
/// uploaded through the module's command so they land in the configured file store.
/// </summary>
internal static class AttachmentsDemoData
{
    /// <summary>Marker: when a file with this name exists the demo data is already in place.</summary>
    public const string MarkerFileName = "filo-takip-teklif-notlari.txt";

    /// <param name="Deal">Title of the company's deal to attach to; otherwise the file goes on the company.</param>
    internal sealed record DemoFile(string TaxNumber, string FileName, string Content, string? Deal = null);

    public static readonly IReadOnlyList<DemoFile> Files =
    [
        new("4610000001", MarkerFileName,
            "Teklif notları\n- 140 araç, 3 yıllık lisans\n- Entegrasyon: ERP, yakıt kartı\n- İndirim en fazla %5\n",
            Deal: "Filo takip yazılımı lisansı"),
        new("4610000001", "arac-listesi.csv", "plaka;model;yil\n34 ABC 101;Ford Transit;2022\n34 ABC 102;Fiat Doblo;2021\n",
            Deal: "Filo takip yazılımı lisansı"),
        new("4610000005", "santiye-gereksinimleri.txt", "Şantiye paneli gereksinimleri\n- Günlük ilerleme raporu\n- Malzeme takibi\n",
            Deal: "Şantiye yönetim paneli"),
        new("4610000002", "nda-imzali.txt", "Gizlilik sözleşmesi imzalandı (demo dosyası).\n"),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AttachmentsDbContext>();
        var crm = services.GetRequiredService<ICrmDirectory>();
        var sales = services.GetRequiredService<ISalesDirectory>();
        var sender = services.GetRequiredService<ISender>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AttachmentsDemoData));

        if(await db.Attachments.AnyAsync(x => x.FileName == MarkerFileName, cancellationToken))
        {
            return;
        }

        var created = 0;
        foreach(var demo in Files)
        {
            if(await crm.FindCompanyByTaxNumberAsync(demo.TaxNumber, cancellationToken) is not { } company)
            {
                continue; // The Crm demo data isn't there; skip rather than fail.
            }

            var (recordType, recordId) = (RecordTypes.Company, company.Id);
            if(demo.Deal is not null
               && (await sales.GetDealsOfCompanyAsync(company.Id, cancellationToken)).FirstOrDefault(d => d.Title == demo.Deal) is { } deal)
            {
                (recordType, recordId) = (RecordTypes.Deal, deal.Id);
            }

            var bytes = Encoding.UTF8.GetBytes(demo.Content);
            using var content = new MemoryStream(bytes);
            await sender.Send(new UploadAttachment(recordType, recordId, demo.FileName, bytes.Length, content), cancellationToken);
            created++;
        }

        logger.LogInformation("Seeded {Files} demo files", created);
    }
}
