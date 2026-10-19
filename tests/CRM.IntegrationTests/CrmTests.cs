using System.Net;
using System.Net.Http.Json;
using CRM.IntegrationTests.Infrastructure;
using CRM.Modules.Crm.Contracts;

namespace CRM.IntegrationTests;

public class CrmTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task CreateCompany_InvalidInput_ReturnsFieldErrors()
    {
        var admin = await AsAdminAsync();

        var problem = await AssertProblemAsync(
            await admin.PostAsJsonAsync("/api/companies", new { companyName = "", taxNumber = "123456789012" }),
            HttpStatusCode.BadRequest, "validation_failed");

        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("companyName", out _));
        Assert.True(errors.TryGetProperty("taxNumber", out _));
    }

    [Fact]
    public async Task CreateCompany_DuplicateTaxNumber_Conflicts_ButMissingTaxNumbersDoNot()
    {
        var admin = await AsAdminAsync();
        var taxNumber = UniqueTaxNumber();

        await CreateCompanyAsync(admin, taxNumber: taxNumber);
        await AssertProblemAsync(
            await admin.PostAsJsonAsync("/api/companies", new { companyName = "Copy", taxNumber }),
            HttpStatusCode.Conflict, "company.tax_number_taken");

        await CreateCompanyAsync(admin);
        await CreateCompanyAsync(admin);
    }

    [Fact]
    public async Task DeleteCompany_WithCustomers_Conflicts()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/customers",
            new { firstName = "Grace", lastName = "Hopper", email = $"{Guid.NewGuid():N}@test.local", companyId })).StatusCode);

        await AssertProblemAsync(await admin.DeleteAsync($"/api/companies/{companyId}"), HttpStatusCode.Conflict, "company.has_customers");
    }

    [Fact]
    public async Task CreateCustomer_UnknownCompany_NotFound()
    {
        var admin = await AsAdminAsync();

        await AssertProblemAsync(
            await admin.PostAsJsonAsync("/api/customers", new { firstName = "A", lastName = "B", email = $"{Guid.NewGuid():N}@test.local", companyId = 999_999 }),
            HttpStatusCode.NotFound, "company.not_found");
    }

    [Fact]
    public async Task ListCompanies_SearchesSortsAndPages()
    {
        var admin = await AsAdminAsync();
        var marker = Unique("Zeta");

        foreach(var suffix in new[] { "b", "a", "c" })
        {
            await CreateCompanyAsync(admin, $"{marker} {suffix}");
        }

        var page = await ReadJsonAsync(await admin.GetAsync(
            $"/api/companies?search={marker.ToLowerInvariant()}&sortBy=companyName&sortDirection=desc&pageSize=2&pageNumber=1"));

        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal([$"{marker} c", $"{marker} b"], page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("companyName").GetString()));

        await AssertProblemAsync(await admin.GetAsync("/api/companies?sortBy=passwordHash"), HttpStatusCode.BadRequest, "query.invalid_sort_field");
    }

    [Fact]
    public async Task UpdateCompany_IsAudited_WithOldAndNewValues()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin, "Before");

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/companies/{companyId}", new { companyName = "After" })).StatusCode);
        // A no-op update must not produce a history entry.
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/companies/{companyId}", new { companyName = "After" })).StatusCode);

        var history = await ReadJsonAsync(await admin.GetAsync($"/api/companies/{companyId}/history"));
        var entry = Assert.Single(history.GetProperty("items").EnumerateArray());

        Assert.Equal("Updated", entry.GetProperty("action").GetString());
        var change = entry.GetProperty("changes").GetProperty("companyName");
        Assert.Equal("Before", change.GetProperty("old").GetString());
        Assert.Equal("After", change.GetProperty("new").GetString());
        Assert.Equal(JsonValueKindNumber, entry.GetProperty("userId").ValueKind);
    }

    [Fact]
    public async Task DeleteCustomer_IsSoft_HidesTheRecord_AndFreesTheEmail()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var email = $"{Guid.NewGuid():N}@test.local";

        var created = await ReadJsonAsync(await admin.PostAsJsonAsync("/api/customers", new { firstName = "A", lastName = "B", email, companyId }));
        var customerId = created.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/customers/{customerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/customers/{customerId}")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/customers", new { firstName = "A", lastName = "B", email, companyId })).StatusCode);

        var history = await ReadJsonAsync(await admin.GetAsync($"/api/customers/{customerId}/history"));
        Assert.Equal("Deleted", history.GetProperty("items")[0].GetProperty("action").GetString());
    }

    [Fact]
    public async Task CreateCompany_PublishesCompanyCreated_ThroughTheOutbox()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin, Unique("Outbox"));

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while(DateTime.UtcNow < deadline && !Factory.DeliveredEvents.OfType<CompanyCreated>().Any(e => e.CompanyId == companyId))
        {
            await Task.Delay(100);
        }

        Assert.Contains(Factory.DeliveredEvents.OfType<CompanyCreated>(), e => e.CompanyId == companyId);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNumber = System.Text.Json.JsonValueKind.Number;
}
