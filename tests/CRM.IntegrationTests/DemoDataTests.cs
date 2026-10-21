using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.BuildingBlocks.Modules;
using CRM.Modules.Activities.Infrastructure.Persistence;
using CRM.Modules.Attachments.Infrastructure.Persistence;
using CRM.IntegrationTests.Infrastructure;
using CRM.Modules.Crm.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Persistence;
using CRM.Modules.Sales.Infrastructure.Persistence;
using CRM.Modules.Tasks.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.IntegrationTests;

/// <summary>Runs the API on its own database with <c>Seed:DemoData</c> on.</summary>
public sealed class DemoDataApiFactory : CrmApiFactory
{
    protected override bool SeedDemoData => true;
}

[CollectionDefinition(Name)]
public sealed class DemoDataCollection : ICollectionFixture<DemoDataApiFactory>
{
    public const string Name = "demo-data";
}

[Collection(DemoDataCollection.Name)]
public class DemoDataTests(DemoDataApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    private async Task<HttpClient> AsAsync(string email, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private Task<HttpClient> AsAdminAsync() => AsAsync(CrmApiFactory.AdminEmail, CrmApiFactory.AdminPassword);

    private static async Task<int> TotalAsync(HttpClient client, string url) =>
        (await client.GetFromJsonAsync<JsonElement>(url, Json)).GetProperty("totalCount").GetInt32();

    [Fact]
    public async Task Companies_And_Customers_AreSeeded_WithTheirStatuses()
    {
        var admin = await AsAdminAsync();

        Assert.Equal(CrmDemoData.Companies.Count, await TotalAsync(admin, "/api/companies"));
        Assert.Equal(CrmDemoData.Companies.Count(c => !c.Active), await TotalAsync(admin, "/api/companies?isActive=false"));
        Assert.Equal(CrmDemoData.CustomerCount, await TotalAsync(admin, "/api/customers"));
        Assert.Equal(
            CrmDemoData.Companies.Sum(c => c.Customers.Count(x => !x.Active)),
            await TotalAsync(admin, "/api/customers?isActive=false"));

        // Turkish characters are kept in names; emails are ASCII.
        var search = await admin.GetFromJsonAsync<JsonElement>("/api/customers?search=irem", Json);
        var irem = Assert.Single(search.GetProperty("items").EnumerateArray());
        Assert.Equal("İrem", irem.GetProperty("firstName").GetString());
        Assert.Equal("irem.gunes@marmara-yapi.test", irem.GetProperty("email").GetString());
    }

    [Fact]
    public async Task DemoUsers_CanSignIn_WithTheirRolesAndLanguage()
    {
        var manager = await AsAsync(IdentityDemoData.MarkerEmail, IdentityDemoData.DefaultPassword);
        var me = await manager.GetFromJsonAsync<JsonElement>("/api/auth/me", Json);

        Assert.Contains(IdentityDemoData.SalesManagerRole, me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        var permissions = me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains("crm.companies.delete", permissions);
        Assert.Contains("identity.users.read", permissions);
        Assert.Contains("sales.stages.manage", permissions);
        Assert.Contains("activities.activities.manage", permissions);
        Assert.Contains("tasks.tasks.manage", permissions);
        Assert.DoesNotContain("identity.users.manage", permissions);

        var elif = await (await AsAsync("elif.demir@demo.local", IdentityDemoData.DefaultPassword)).GetFromJsonAsync<JsonElement>("/api/auth/me", Json);
        Assert.Equal("en", elif.GetProperty("preferredLanguage").GetString());

        // The inactive demo user is refused.
        var refused = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "can.ozturk@demo.local", password = IdentityDemoData.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task Deals_AreSeeded_AcrossThePipeline_InEveryCurrency()
    {
        var admin = await AsAdminAsync();

        Assert.Equal(SalesDemoData.Deals.Count, await TotalAsync(admin, "/api/deals"));
        Assert.Equal(SalesDemoData.Deals.Count(d => d.Status == "won"), await TotalAsync(admin, "/api/deals?status=won"));
        Assert.Equal(SalesDemoData.Deals.Count(d => d.Status == "lost"), await TotalAsync(admin, "/api/deals?status=lost"));

        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/deals/summary", Json);
        Assert.Equal(["EUR", "TRY", "USD"], summary.GetProperty("open").EnumerateArray().Select(t => t.GetProperty("currency").GetString()));
        Assert.Contains(summary.GetProperty("wonThisMonth").EnumerateArray(), t => t.GetProperty("currency").GetString() == "TRY");

        var marker = await admin.GetFromJsonAsync<JsonElement>($"/api/deals?search={Uri.EscapeDataString(SalesDemoData.MarkerTitle)}", Json);
        var deal = Assert.Single(marker.GetProperty("items").EnumerateArray());
        Assert.Equal("Anadolu Lojistik A.Ş.", deal.GetProperty("companyName").GetString());
        Assert.Equal("Hakan Arslan", deal.GetProperty("customerName").GetString());
    }

    [Fact]
    public async Task Activities_AreSeeded_OnDemoCompaniesContactsAndDeals()
    {
        var admin = await AsAdminAsync();

        Assert.Equal(ActivitiesDemoData.Activities.Count, await TotalAsync(admin, "/api/activities"));

        var marker = await admin.GetFromJsonAsync<JsonElement>($"/api/activities?search={Uri.EscapeDataString(ActivitiesDemoData.MarkerSubject)}", Json);
        var activity = Assert.Single(marker.GetProperty("items").EnumerateArray());
        Assert.Equal("meeting", activity.GetProperty("type").GetString());
        Assert.Equal("Anadolu Lojistik A.Ş.", activity.GetProperty("companyName").GetString());
        Assert.Equal("Hakan Arslan", activity.GetProperty("customerName").GetString());
        Assert.Equal("Filo takip yazılımı lisansı", activity.GetProperty("dealTitle").GetString());

        // One planned (future) meeting.
        var items = (await admin.GetFromJsonAsync<JsonElement>("/api/activities?pageSize=100", Json)).GetProperty("items").EnumerateArray();
        Assert.Contains(items, a => a.GetProperty("occurredAt").GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task Tasks_AreSeeded_ForDemoUsers()
    {
        Assert.Equal(TasksDemoData.Tasks.Count, await TotalAsync(await AsAdminAsync(), "/api/tasks"));

        var manager = await AsAsync(IdentityDemoData.MarkerEmail, IdentityDemoData.DefaultPassword);
        var mine = await manager.GetFromJsonAsync<JsonElement>("/api/tasks?mine=true", Json);
        var first = mine.GetProperty("items")[0];
        Assert.Equal(TasksDemoData.MarkerTitle, first.GetProperty("title").GetString()); // overdue, so first
        Assert.Equal("Filo takip yazılımı lisansı", first.GetProperty("dealTitle").GetString());
        Assert.Equal("Ayşe Yılmaz", first.GetProperty("assigneeName").GetString());

        var summary = await manager.GetFromJsonAsync<JsonElement>("/api/tasks/my-summary", Json);
        Assert.Equal(TasksDemoData.Tasks.Count(t => t.Assignee == IdentityDemoData.MarkerEmail && !t.Done), summary.GetProperty("open").GetInt32());
        Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(1, summary.GetProperty("dueToday").GetInt32());
        Assert.Equal(TasksDemoData.Tasks.Count(t => t.Done), await TotalAsync(await AsAdminAsync(), "/api/tasks?status=done"));
    }

    [Fact]
    public async Task DemoUsers_AreNotifiedOfTheTasksAssignedToThem()
    {
        var manager = await AsAsync(IdentityDemoData.MarkerEmail, IdentityDemoData.DefaultPassword);
        var expected = TasksDemoData.Tasks.Count(t => t.Assignee == IdentityDemoData.MarkerEmail);

        // Delivered through the outbox right after startup.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        JsonElement page;
        do
        {
            page = await manager.GetFromJsonAsync<JsonElement>("/api/notifications", Json);
            if(page.GetProperty("totalCount").GetInt32() >= expected) break;
            await Task.Delay(200);
        } while(DateTime.UtcNow < deadline);

        Assert.Equal(expected, page.GetProperty("totalCount").GetInt32());
        Assert.All(page.GetProperty("items").EnumerateArray(), n => Assert.Equal("task.assigned", n.GetProperty("type").GetString()));
        Assert.Contains(page.GetProperty("items").EnumerateArray(), n => n.GetProperty("subject").GetString() == TasksDemoData.MarkerTitle);
    }

    [Fact]
    public async Task Files_AreSeeded_OnDemoDealsAndCompanies_AndCanBeDownloaded()
    {
        var admin = await AsAdminAsync();
        var deals = await admin.GetFromJsonAsync<JsonElement>($"/api/deals?search={Uri.EscapeDataString(SalesDemoData.MarkerTitle)}", Json);
        var dealId = deals.GetProperty("items")[0].GetProperty("id").GetInt32();

        var files = await admin.GetFromJsonAsync<JsonElement>($"/api/attachments?recordType=deal&recordId={dealId}", Json);
        Assert.Equal(AttachmentsDemoData.Files.Count(f => f.Deal == SalesDemoData.MarkerTitle), files.GetArrayLength());

        var marker = files.EnumerateArray().Single(f => f.GetProperty("fileName").GetString() == AttachmentsDemoData.MarkerFileName);
        var content = await admin.GetStringAsync($"/api/attachments/{marker.GetProperty("id").GetInt32()}/download");
        Assert.StartsWith("Teklif notları", content);
    }

    [Fact]
    public async Task SalesReport_ShowsTheDemoHistory_OverTheLastSixMonths()
    {
        var report = await (await AsAdminAsync()).GetFromJsonAsync<JsonElement>("/api/reports/sales", Json);

        var closedInPeriod = SalesDemoData.Deals.Where(d => d.Status != "open" && (d.ClosedMonthsAgo ?? 0) <= 5).ToList();
        Assert.Equal(closedInPeriod.Count(d => d.Status == "won"), report.GetProperty("wonCount").GetInt32());
        Assert.Equal(closedInPeriod.Count(d => d.Status == "lost"), report.GetProperty("lostCount").GetInt32());

        // Wins are spread over several months, in every currency.
        Assert.True(report.GetProperty("months").EnumerateArray().Count(m => m.GetProperty("won").GetArrayLength() > 0) >= 4);
        Assert.Equal(["EUR", "TRY", "USD"], report.GetProperty("won").EnumerateArray().Select(c => c.GetProperty("currency").GetString()));
        Assert.True(report.GetProperty("averageCycleDays").GetDouble() > 0);
        var price = report.GetProperty("lostReasons").EnumerateArray().Single(r => r.GetProperty("reason").GetString() == "Fiyat yüksek bulundu.");
        Assert.Equal(2, price.GetProperty("count").GetInt32());
        Assert.Equal(2, report.GetProperty("lostReasons")[0].GetProperty("count").GetInt32()); // most frequent first
    }

    [Fact]
    public async Task Seeding_IsIdempotent()
    {
        var admin = await AsAdminAsync();
        var companiesBefore = await TotalAsync(admin, "/api/companies");
        var usersBefore = await TotalAsync(admin, "/api/users");
        var dealsBefore = await TotalAsync(admin, "/api/deals");
        var activitiesBefore = await TotalAsync(admin, "/api/activities");
        var tasksBefore = await TotalAsync(admin, "/api/tasks");

        await ModuleHostExtensions.SeedDemoDataAsync(factory.Services);

        Assert.Equal(companiesBefore, await TotalAsync(admin, "/api/companies"));
        Assert.Equal(usersBefore, await TotalAsync(admin, "/api/users"));
        Assert.Equal(dealsBefore, await TotalAsync(admin, "/api/deals"));
        Assert.Equal(activitiesBefore, await TotalAsync(admin, "/api/activities"));
        Assert.Equal(tasksBefore, await TotalAsync(admin, "/api/tasks"));
    }

    [Fact]
    public async Task DemoData_IsCreatedThroughCommands_SoAuditFieldsAndEventsExist()
    {
        var admin = await AsAdminAsync();

        // The inactive demo company was deactivated with a command, so its history records it.
        var inactive = await admin.GetFromJsonAsync<JsonElement>("/api/companies?isActive=false", Json);
        var companyId = inactive.GetProperty("items")[0].GetProperty("id").GetInt32();
        var history = await admin.GetFromJsonAsync<JsonElement>($"/api/companies/{companyId}/history", Json);

        Assert.Contains(history.GetProperty("items").EnumerateArray(), e =>
            e.GetProperty("changes").TryGetProperty("isActive", out _));
    }
}
