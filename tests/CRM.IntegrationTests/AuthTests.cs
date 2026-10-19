using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using CRM.IntegrationTests.Infrastructure;

namespace CRM.IntegrationTests;

public class AuthTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Register_ThenLogin_GrantsDefaultUserPermissions()
    {
        var (email, password) = await RegisterUserAsync();

        var tokens = await LoginAsync(email, password);
        var user = tokens.GetProperty("user");

        Assert.Equal(["User"], user.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        var permissions = user.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("crm.companies.read", permissions);
        Assert.DoesNotContain("crm.companies.delete", permissions);
        Assert.DoesNotContain("identity.users.manage", permissions);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.GetProperty("accessToken").GetString());
        Assert.Equal(email, jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("tr", jwt.Claims.Single(c => c.Type == "locale").Value);
    }

    [Fact]
    public async Task Register_CannotChooseItsRole()
    {
        var email = $"sneaky-{Guid.NewGuid():N}@test.local";

        var response = await Anonymous().PostAsJsonAsync("/api/auth/register",
            new { firstName = "A", lastName = "B", email, password = "Passw0rd-123", roleName = "Admin", roleIds = new[] { 1 } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var roles = (await ReadJsonAsync(response)).GetProperty("roles").EnumerateArray().Select(x => x.GetString());
        Assert.Equal(["User"], roles);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorizedProblem()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login",
            new { email = CrmApiFactory.AdminEmail, password = "wrong-password" });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.invalid_credentials");
    }

    [Fact]
    public async Task Refresh_RotatesTokens_AndReusingAnOldOneEndsAllSessions()
    {
        var (email, password) = await RegisterUserAsync();
        var first = (await LoginAsync(email, password)).GetProperty("refreshToken").GetString();

        var refreshed = await Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = (await ReadJsonAsync(refreshed)).GetProperty("refreshToken").GetString();
        Assert.NotEqual(first, second);

        // Replaying the rotated token is treated as theft...
        await AssertProblemAsync(
            await Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first }),
            HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");

        // ...so the legitimate newer token is revoked too.
        await AssertProblemAsync(
            await Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second }),
            HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var (email, password) = await RegisterUserAsync();
        var refreshToken = (await LoginAsync(email, password)).GetProperty("refreshToken").GetString();

        Assert.Equal(HttpStatusCode.NoContent,
            (await Anonymous().PostAsJsonAsync("/api/auth/logout", new { refreshToken })).StatusCode);

        await AssertProblemAsync(
            await Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken }),
            HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
    }

    [Fact]
    public async Task Preferences_ValidatesLanguage_AndAreUsedInTheNextToken()
    {
        var (email, password) = await RegisterUserAsync();
        var client = await AsUserAsync(email, password);

        var invalid = await client.PutAsJsonAsync("/api/auth/me/preferences", new { preferredLanguage = "de" });
        var problem = await AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("preferredLanguage", out _));

        var valid = await client.PutAsJsonAsync("/api/auth/me/preferences", new { preferredLanguage = "en" });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken((await LoginAsync(email, password)).GetProperty("accessToken").GetString());
        Assert.Equal("en", jwt.Claims.Single(c => c.Type == "locale").Value);
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword_AndNewOneWorks()
    {
        var (email, password) = await RegisterUserAsync();
        var client = await AsUserAsync(email, password);

        await AssertProblemAsync(
            await client.PostAsJsonAsync("/api/auth/me/change-password", new { currentPassword = "nope-nope", newPassword = "NewPassw0rd-1" }),
            HttpStatusCode.BadRequest, "auth.wrong_password");

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/me/change-password", new { currentPassword = password, newPassword = "NewPassw0rd-1" })).StatusCode);

        await LoginAsync(email, "NewPassw0rd-1");
    }
}
