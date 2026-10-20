using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.BuildingBlocks.Modules;
using CRM.IntegrationTests.Infrastructure;
using CRM.Modules.Crm.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Persistence;
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
        Assert.DoesNotContain("identity.users.manage", permissions);

        var elif = await (await AsAsync("elif.demir@demo.local", IdentityDemoData.DefaultPassword)).GetFromJsonAsync<JsonElement>("/api/auth/me", Json);
        Assert.Equal("en", elif.GetProperty("preferredLanguage").GetString());

        // The inactive demo user is refused.
        var refused = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "can.ozturk@demo.local", password = IdentityDemoData.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task Seeding_IsIdempotent()
    {
        var admin = await AsAdminAsync();
        var companiesBefore = await TotalAsync(admin, "/api/companies");
        var usersBefore = await TotalAsync(admin, "/api/users");

        await ModuleHostExtensions.SeedDemoDataAsync(factory.Services);

        Assert.Equal(companiesBefore, await TotalAsync(admin, "/api/companies"));
        Assert.Equal(usersBefore, await TotalAsync(admin, "/api/users"));
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
