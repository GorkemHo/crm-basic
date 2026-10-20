using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.IntegrationTests.Infrastructure;

namespace CRM.IntegrationTests;

public class AuthorizationTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Anonymous_GetsUnauthorizedProblem()
    {
        await AssertProblemAsync(await Anonymous().GetAsync("/api/companies"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task User_CanReadAndWriteCompanies_ButNotDeleteOrManageUsers()
    {
        var (email, password) = await RegisterUserAsync();
        var user = await AsUserAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/companies")).StatusCode);
        var companyId = await CreateCompanyAsync(user);

        await AssertProblemAsync(await user.DeleteAsync($"/api/companies/{companyId}"), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(await user.GetAsync("/api/users"), HttpStatusCode.Forbidden, "forbidden");

        var admin = await AsAdminAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/companies/{companyId}")).StatusCode);
    }

    [Fact]
    public async Task GrantingAPermissionToARole_AppliesAfterTokenRefresh()
    {
        var admin = await AsAdminAsync();

        var roleResponse = await admin.PostAsJsonAsync("/api/roles", new { name = Unique("Deleters") });
        Assert.Equal(HttpStatusCode.Created, roleResponse.StatusCode);
        var roleId = (await ReadJsonAsync(roleResponse)).GetProperty("id").GetInt32();

        var (email, password) = await RegisterUserAsync();
        var tokens = await LoginAsync(email, password);
        var userId = tokens.GetProperty("user").GetProperty("id").GetInt32();
        var user = WithToken(tokens.GetProperty("accessToken").GetString()!);
        var companyId = await CreateCompanyAsync(user);

        var userRoleId = await GetRoleIdAsync(admin, "User");
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PutAsJsonAsync($"/api/users/{userId}/roles", new { roleIds = new[] { userRoleId, roleId } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new { permissions = new[] { "crm.companies.delete" } })).StatusCode);

        // The old access token still carries the old permissions...
        Assert.Equal(HttpStatusCode.Forbidden, (await user.DeleteAsync($"/api/companies/{companyId}")).StatusCode);

        // ...a refreshed one has the new permission.
        var refreshed = await ReadJsonAsync(await Anonymous().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = tokens.GetProperty("refreshToken").GetString() }));
        var refreshedUser = WithToken(refreshed.GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.NoContent, (await refreshedUser.DeleteAsync($"/api/companies/{companyId}")).StatusCode);
    }

    [Fact]
    public async Task AdminRole_PermissionsCannotBeEdited_AndBuiltInRolesCannotBeDeleted()
    {
        var admin = await AsAdminAsync();
        var adminRoleId = await GetRoleIdAsync(admin, "Admin");

        await AssertProblemAsync(
            await admin.PutAsJsonAsync($"/api/roles/{adminRoleId}/permissions", new { permissions = Array.Empty<string>() }),
            HttpStatusCode.UnprocessableEntity, "role.admin_permissions_fixed");

        await AssertProblemAsync(await admin.DeleteAsync($"/api/roles/{adminRoleId}"), HttpStatusCode.UnprocessableEntity, "role.system_role");
    }

    [Fact]
    public async Task PermissionCatalog_ListsEveryModulesPermissions()
    {
        var permissions = await ReadJsonAsync(await (await AsAdminAsync()).GetAsync("/api/permissions"));
        var modules = permissions.EnumerateArray().Select(p => p.GetProperty("module").GetString()).Distinct().ToList();

        Assert.Contains("crm", modules);
        Assert.Contains("identity", modules);
        Assert.Contains("sales", modules);
        Assert.Contains("activities", modules);
        Assert.Contains("tasks", modules);
        Assert.Contains("attachments", modules);
    }

    private static async Task<int> GetRoleIdAsync(HttpClient admin, string name)
    {
        var roles = await admin.GetFromJsonAsync<JsonElement>("/api/roles", Json);
        return roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == name).GetProperty("id").GetInt32();
    }
}
