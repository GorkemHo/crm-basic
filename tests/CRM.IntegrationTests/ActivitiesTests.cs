using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.IntegrationTests.Infrastructure;

namespace CRM.IntegrationTests;

public class ActivitiesTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task LogActivity_WithContactAndDeal_ReturnsNamesAndAuthor()
    {
        var admin = await AsAdminAsync();
        var companyName = Unique("Acme");
        var companyId = await CreateCompanyAsync(admin, companyName);
        var customerId = await CreateCustomerAsync(admin, companyId);
        var dealId = await CreateDealAsync(admin, companyId, "Big deal");

        var activity = await LogAsync(admin, companyId, type: "call", customerId: customerId, dealId: dealId, durationMinutes: 15);

        Assert.Equal("call", activity.GetProperty("type").GetString());
        Assert.Equal(companyName, activity.GetProperty("companyName").GetString());
        Assert.Equal("Ada Lovelace", activity.GetProperty("customerName").GetString());
        Assert.Equal("Big deal", activity.GetProperty("dealTitle").GetString());
        Assert.Equal(15, activity.GetProperty("durationMinutes").GetInt32());

        var me = await ReadJsonAsync(await admin.GetAsync("/api/auth/me"));
        Assert.Equal(me.GetProperty("id").GetInt32(), activity.GetProperty("createdBy").GetInt32());
        Assert.Equal($"{me.GetProperty("firstName").GetString()} {me.GetProperty("lastName").GetString()}",
            activity.GetProperty("createdByName").GetString());
    }

    [Fact]
    public async Task LogActivity_InvalidInput_ReturnsFieldErrors()
    {
        var admin = await AsAdminAsync();

        var problem = await AssertProblemAsync(
            await admin.PostAsJsonAsync("/api/activities", new { type = "fax", subject = "", occurredAt = DateTime.UtcNow, companyId = 1, durationMinutes = 0 }),
            HttpStatusCode.BadRequest, "validation_failed");

        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("type", out _));
        Assert.True(errors.TryGetProperty("subject", out _));
        Assert.True(errors.TryGetProperty("durationMinutes", out _));
    }

    [Fact]
    public async Task LogActivity_ContactAndDealMustBelongToTheCompany()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var otherCompanyId = await CreateCompanyAsync(admin);
        var otherCustomer = await CreateCustomerAsync(admin, otherCompanyId);
        var otherDeal = await CreateDealAsync(admin, otherCompanyId);

        await AssertProblemAsync(await PostAsync(admin, 999_999), HttpStatusCode.NotFound, "company.not_found");
        await AssertProblemAsync(await PostAsync(admin, companyId, customerId: otherCustomer),
            HttpStatusCode.UnprocessableEntity, "activity.contact_not_in_company");
        await AssertProblemAsync(await PostAsync(admin, companyId, dealId: otherDeal),
            HttpStatusCode.UnprocessableEntity, "activity.deal_not_in_company");
        await AssertProblemAsync(await PostAsync(admin, companyId, dealId: 999_999), HttpStatusCode.NotFound, "deal.not_found");
    }

    [Fact]
    public async Task Notes_HaveNoDuration()
    {
        var admin = await AsAdminAsync();
        var note = await LogAsync(admin, await CreateCompanyAsync(admin), type: "note", durationMinutes: 30);

        Assert.Equal(JsonValueKind.Null, note.GetProperty("durationMinutes").ValueKind);
    }

    [Fact]
    public async Task OnlyTheAuthorOrAManager_CanEditOrDelete()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);

        var (authorEmail, authorPassword) = await RegisterUserAsync();
        var author = await AsUserAsync(authorEmail, authorPassword);
        var (otherEmail, otherPassword) = await RegisterUserAsync();
        var other = await AsUserAsync(otherEmail, otherPassword);

        var activity = await LogAsync(author, companyId, subject: "Mine");
        var id = activity.GetProperty("id").GetInt32();
        var body = new { type = "note", subject = "Changed", occurredAt = DateTime.UtcNow, companyId };

        await AssertProblemAsync(await other.PutAsJsonAsync($"/api/activities/{id}", body), HttpStatusCode.Forbidden, "activity.not_author");
        await AssertProblemAsync(await other.DeleteAsync($"/api/activities/{id}"), HttpStatusCode.Forbidden, "activity.not_author");

        var edited = await ReadJsonAsync(await author.PutAsJsonAsync($"/api/activities/{id}", body));
        Assert.Equal("Changed", edited.GetProperty("subject").GetString());

        // Admin has activities.activities.manage.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/activities/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/activities/{id}")).StatusCode);
    }

    [Fact]
    public async Task ListActivities_FiltersByCompanyDealAndType_NewestFirst()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var dealId = await CreateDealAsync(admin, companyId);
        var now = DateTime.UtcNow;

        await LogAsync(admin, companyId, subject: "old", occurredAt: now.AddDays(-3));
        await LogAsync(admin, companyId, subject: "newest", occurredAt: now, dealId: dealId, type: "meeting");
        await LogAsync(admin, companyId, subject: "middle", occurredAt: now.AddDays(-1), dealId: dealId);

        var all = await ReadJsonAsync(await admin.GetAsync($"/api/activities?companyId={companyId}"));
        Assert.Equal(["newest", "middle", "old"], Subjects(all));

        var ofDeal = await ReadJsonAsync(await admin.GetAsync($"/api/activities?dealId={dealId}"));
        Assert.Equal(["newest", "middle"], Subjects(ofDeal));

        var meetings = await ReadJsonAsync(await admin.GetAsync($"/api/activities?companyId={companyId}&type=meeting"));
        Assert.Equal(["newest"], Subjects(meetings));

        static IEnumerable<string?> Subjects(JsonElement page) =>
            page.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task RecordsWithActivities_CannotBeDeleted()
    {
        var admin = await AsAdminAsync();

        var companyId = await CreateCompanyAsync(admin);
        await LogAsync(admin, companyId);
        await AssertProblemAsync(await admin.DeleteAsync($"/api/companies/{companyId}"), HttpStatusCode.Conflict, "company.has_activities");

        var otherCompany = await CreateCompanyAsync(admin);
        var customerId = await CreateCustomerAsync(admin, otherCompany);
        var dealId = await CreateDealAsync(admin, otherCompany);
        await LogAsync(admin, otherCompany, customerId: customerId, dealId: dealId);

        await AssertProblemAsync(await admin.DeleteAsync($"/api/customers/{customerId}"), HttpStatusCode.Conflict, "customer.has_activities");
        await AssertProblemAsync(await admin.DeleteAsync($"/api/deals/{dealId}"), HttpStatusCode.Conflict, "deal.has_activities");
    }

    // ---------------------------------------------------------------- Helpers

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, int companyId, string type = "note", string? subject = null, DateTime? occurredAt = null,
        int? customerId = null, int? dealId = null, int? durationMinutes = null) =>
        client.PostAsJsonAsync("/api/activities", new
        {
            type,
            subject = subject ?? Unique("Activity"),
            notes = "Some notes",
            occurredAt = occurredAt ?? DateTime.UtcNow,
            durationMinutes,
            companyId,
            customerId,
            dealId
        });

    private static async Task<JsonElement> LogAsync(
        HttpClient client, int companyId, string type = "note", string? subject = null, DateTime? occurredAt = null,
        int? customerId = null, int? dealId = null, int? durationMinutes = null)
    {
        var response = await PostAsync(client, companyId, type, subject, occurredAt, customerId, dealId, durationMinutes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<int> CreateCustomerAsync(HttpClient client, int companyId)
    {
        var response = await client.PostAsJsonAsync("/api/customers",
            new { firstName = "Ada", lastName = "Lovelace", email = $"{Guid.NewGuid():N}@test.local", companyId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    private static async Task<int> CreateDealAsync(HttpClient client, int companyId, string? title = null)
    {
        var response = await client.PostAsJsonAsync("/api/deals",
            new { title = title ?? Unique("Deal"), companyId, amount = 100m, currency = "TRY" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }
}
