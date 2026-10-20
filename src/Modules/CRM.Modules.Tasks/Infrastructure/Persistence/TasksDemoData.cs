using CRM.BuildingBlocks.Application;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Tasks.Domain;
using CRM.Modules.Tasks.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Tasks.Infrastructure.Persistence;

/// <summary>
/// Demo tasks for the Identity demo users (found by email) on the Crm demo companies and Sales demo deals:
/// overdue, due today, upcoming, without a date and done. Created through the module's commands.
/// </summary>
internal static class TasksDemoData
{
    /// <summary>Marker: when a task with this title exists the demo data is already in place.</summary>
    public const string MarkerTitle = "Filo takip teklifini revize et";

    /// <param name="DueInDays">Relative to today; negative = overdue.</param>
    /// <param name="Contact">Index into the company's customers.</param>
    /// <param name="Deal">Title of the company's deal to link to.</param>
    internal sealed record DemoTask(
        string Assignee, string Title, string Priority, int? DueInDays, string? TaxNumber = null,
        int? Contact = null, string? Deal = null, bool Done = false, string? Description = null);

    public static readonly IReadOnlyList<DemoTask> Tasks =
    [
        new("ayse.yilmaz@demo.local", MarkerTitle, TaskPriorities.High, -2, "4610000001", Contact: 0, Deal: "Filo takip yazılımı lisansı",
            Description: "Toplantıda istenen entegrasyon kalemlerini ekle, indirimi %5 ile sınırla."),
        new("ayse.yilmaz@demo.local", "Bulut geçişi pilot planını paylaş", TaskPriorities.Normal, 0, "4610000002", Contact: 0, Deal: "Bulut altyapı geçişi"),
        new("ayse.yilmaz@demo.local", "Çeyrek satış raporunu hazırla", TaskPriorities.Normal, 7),
        new("mehmet.kaya@demo.local", "Şantiye paneli için demo ortamı kur", TaskPriorities.High, 3, "4610000005", Contact: 3, Deal: "Şantiye yönetim paneli"),
        new("mehmet.kaya@demo.local", "ERP entegrasyonu kapsam dokümanı", TaskPriorities.Low, null, "4610000003", Deal: "ERP entegrasyonu"),
        new("mehmet.kaya@demo.local", "Soğuk zincir kurulum tarihini teyit et", TaskPriorities.Normal, -5, "4610000004", Contact: 0, Done: true),
        new("elif.demir@demo.local", "Send the reference list to Toros", TaskPriorities.Normal, 1, "4610000008", Contact: 1),
        new("elif.demir@demo.local", "Call Akdeniz Turizm again in September", TaskPriorities.Low, 45, "4610000006", Contact: 1),
        new("elif.demir@demo.local", "Prepare the season kick-off call", TaskPriorities.High, -1, "4610000012", Contact: 1, Deal: "Sezonluk kampanya CRM kurulumu"),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<TasksDbContext>();
        var crm = services.GetRequiredService<ICrmDirectory>();
        var sales = services.GetRequiredService<ISalesDirectory>();
        var users = services.GetRequiredService<IUserDirectory>();
        var sender = services.GetRequiredService<ISender>();
        var clock = services.GetRequiredService<TimeProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TasksDemoData));

        if(await db.Tasks.AnyAsync(x => x.Title == MarkerTitle, cancellationToken))
        {
            return;
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var created = 0;

        foreach(var demo in Tasks)
        {
            if(await users.FindByEmailAsync(demo.Assignee, cancellationToken) is not { IsActive: true } assignee)
            {
                continue; // The Identity demo data isn't there; skip rather than fail.
            }

            int? companyId = null, contactId = null, dealId = null;
            if(demo.TaxNumber is not null)
            {
                if(await crm.FindCompanyByTaxNumberAsync(demo.TaxNumber, cancellationToken) is not { } company)
                {
                    continue;
                }

                companyId = company.Id;
                var contacts = await crm.GetCustomersOfCompanyAsync(company.Id, cancellationToken);
                contactId = demo.Contact is { } index && index < contacts.Count ? contacts[index].Id : null;
                dealId = demo.Deal is null ? null
                    : (await sales.GetDealsOfCompanyAsync(company.Id, cancellationToken)).FirstOrDefault(d => d.Title == demo.Deal)?.Id;
            }

            var task = await sender.Send(new CreateTask(
                demo.Title, demo.Description, demo.DueInDays is { } days ? today.AddDays(days) : null, demo.Priority,
                assignee.Id, companyId, contactId, dealId), cancellationToken);

            if(demo.Done)
            {
                await sender.Send(new CompleteTask(task.Id), cancellationToken);
            }

            created++;
        }

        logger.LogInformation("Seeded {Tasks} demo tasks", created);
    }
}
