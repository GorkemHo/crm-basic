using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CRM.IntegrationTests.Infrastructure;

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase
{
    protected static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    protected ApiTestBase(CrmApiFactory factory)
    {
        Factory = factory;
    }

    protected CrmApiFactory Factory { get; }

    protected HttpClient Anonymous() => Factory.CreateClient();

    protected Task<HttpClient> AsAdminAsync() => AsUserAsync(CrmApiFactory.AdminEmail, CrmApiFactory.AdminPassword);

    protected async Task<HttpClient> AsUserAsync(string email, string password)
    {
        var tokens = await LoginAsync(email, password);
        return WithToken(tokens.GetProperty("accessToken").GetString()!);
    }

    protected HttpClient WithToken(string accessToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected async Task<JsonElement> LoginAsync(string email, string password)
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    /// <summary>Registers a self-service user (built-in User role) and returns their credentials.</summary>
    protected async Task<(string Email, string Password)> RegisterUserAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        const string password = "Passw0rd-123";

        var response = await Anonymous().PostAsJsonAsync("/api/auth/register",
            new { firstName = "Test", lastName = "User", email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (email, password);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    /// <summary>Asserts an RFC 7807 problem response with the given status and error code.</summary>
    protected static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadJsonAsync(response);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
        return problem;
    }

    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    protected static async Task<int> CreateCompanyAsync(HttpClient client, string? name = null, string? taxNumber = null)
    {
        var response = await client.PostAsJsonAsync("/api/companies", new { companyName = name ?? Unique("Co"), taxNumber });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    /// <summary>Tax numbers are max 11 characters and must be unique across the shared test database.</summary>
    protected static string UniqueTaxNumber() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();
}
