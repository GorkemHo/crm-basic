using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.IntegrationTests.Infrastructure;

namespace CRM.IntegrationTests;

public class SalesTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    // ---------------------------------------------------------------- Stages

    [Fact]
    public async Task DefaultPipeline_IsCreated_InOrder()
    {
        var stages = await GetStagesAsync(await AsAdminAsync());

        Assert.True(stages.Count >= 4);
        Assert.Equal(Enumerable.Range(0, stages.Count), stages.Select(s => s.GetProperty("position").GetInt32()));
    }

    [Fact]
    public async Task Stages_CanBeAddedRenamedReorderedAndDeleted_ByAdmins()
    {
        var admin = await AsAdminAsync();
        var name = Unique("Demo");

        var created = await ReadJsonAsync(await admin.PostAsJsonAsync("/api/pipeline-stages", new { name, probability = 90 }));
        var id = created.GetProperty("id").GetInt32();
        var before = await GetStagesAsync(admin);
        Assert.Equal(id, before[^1].GetProperty("id").GetInt32());

        await AssertProblemAsync(await admin.PostAsJsonAsync("/api/pipeline-stages", new { name = name.ToUpperInvariant(), probability = 10 }),
            HttpStatusCode.Conflict, "stage.name_taken");
        await AssertProblemAsync(await admin.PostAsJsonAsync("/api/pipeline-stages", new { name = Unique("Bad"), probability = 101 }),
            HttpStatusCode.BadRequest, "validation_failed");

        var badPatch = await AssertProblemAsync(await admin.PatchAsJsonAsync($"/api/pipeline-stages/{id}", new { probability = 150 }),
            HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(badPatch.GetProperty("errors").TryGetProperty("probability", out _));

        var renamed = await ReadJsonAsync(await admin.PatchAsJsonAsync($"/api/pipeline-stages/{id}", new { name = name + "!", probability = 80 }));
        Assert.Equal(80, renamed.GetProperty("probability").GetInt32());

        // Move the new stage to the front.
        var ids = before.Select(s => s.GetProperty("id").GetInt32()).ToList();
        int[] order = [id, .. ids.Where(x => x != id)];
        var reordered = await ReadJsonAsync(await admin.PutAsJsonAsync("/api/pipeline-stages/order", new { stageIds = order }));
        Assert.Equal(order, reordered.EnumerateArray().Select(s => s.GetProperty("id").GetInt32()));

        await AssertProblemAsync(await admin.PutAsJsonAsync("/api/pipeline-stages/order", new { stageIds = order[1..] }),
            HttpStatusCode.BadRequest, "stage.reorder_incomplete");

        // Restore the original order by deleting it: positions close up again.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/pipeline-stages/{id}")).StatusCode);
        Assert.Equal(ids.Where(x => x != id), (await GetStagesAsync(admin)).Select(s => s.GetProperty("id").GetInt32()));
        Assert.Equal(Enumerable.Range(0, ids.Count - 1), (await GetStagesAsync(admin)).Select(s => s.GetProperty("position").GetInt32()));
    }

    [Fact]
    public async Task StageWithDeals_CannotBeDeleted()
    {
        var admin = await AsAdminAsync();
        var stage = await ReadJsonAsync(await admin.PostAsJsonAsync("/api/pipeline-stages", new { name = Unique("Busy"), probability = 40 }));
        var stageId = stage.GetProperty("id").GetInt32();
        var deal = await CreateDealAsync(admin, await CreateCompanyAsync(admin), stageId: stageId);

        await AssertProblemAsync(await admin.DeleteAsync($"/api/pipeline-stages/{stageId}"), HttpStatusCode.Conflict, "stage.has_deals");

        await admin.DeleteAsync($"/api/deals/{deal.GetProperty("id").GetInt32()}");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/pipeline-stages/{stageId}")).StatusCode);
    }

    [Fact]
    public async Task Users_CanWorkWithDeals_ButNotManageStagesOrDeleteDeals()
    {
        var (email, password) = await RegisterUserAsync();
        var user = await AsUserAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/pipeline-stages")).StatusCode);
        await AssertProblemAsync(await user.PostAsJsonAsync("/api/pipeline-stages", new { name = Unique("No"), probability = 1 }),
            HttpStatusCode.Forbidden, "forbidden");

        var deal = await CreateDealAsync(user, await CreateCompanyAsync(user));
        await AssertProblemAsync(await user.DeleteAsync($"/api/deals/{deal.GetProperty("id").GetInt32()}"), HttpStatusCode.Forbidden, "forbidden");
    }

    // ---------------------------------------------------------------- Deals

    [Fact]
    public async Task CreateDeal_StartsInTheFirstStage_WithCompanyAndContactNames()
    {
        var admin = await AsAdminAsync();
        var companyName = Unique("Acme");
        var companyId = await CreateCompanyAsync(admin, companyName);
        var customerId = await CreateCustomerAsync(admin, companyId);

        var deal = await CreateDealAsync(admin, companyId, customerId, amount: 1234.5m, currency: "USD");
        var firstStage = (await GetStagesAsync(admin))[0];

        Assert.Equal(firstStage.GetProperty("id").GetInt32(), deal.GetProperty("stageId").GetInt32());
        Assert.Equal(companyName, deal.GetProperty("companyName").GetString());
        Assert.Equal("Ada Lovelace", deal.GetProperty("customerName").GetString());
        Assert.Equal(1234.5m, deal.GetProperty("amount").GetDecimal());
        Assert.Equal("USD", deal.GetProperty("currency").GetString());
        Assert.Equal("open", deal.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateDeal_InvalidInput_ReturnsFieldErrors()
    {
        var admin = await AsAdminAsync();

        var problem = await AssertProblemAsync(
            await admin.PostAsJsonAsync("/api/deals", new { title = "", companyId = 1, amount = -1m, currency = "GBP" }),
            HttpStatusCode.BadRequest, "validation_failed");

        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("amount", out _));
        Assert.True(errors.TryGetProperty("currency", out _));
    }

    [Fact]
    public async Task CreateDeal_ChecksCompanyAndContact()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var otherCustomer = await CreateCustomerAsync(admin, await CreateCompanyAsync(admin));

        await AssertProblemAsync(await PostDealAsync(admin, 999_999), HttpStatusCode.NotFound, "company.not_found");
        await AssertProblemAsync(await PostDealAsync(admin, companyId, 999_999), HttpStatusCode.NotFound, "customer.not_found");
        await AssertProblemAsync(await PostDealAsync(admin, companyId, otherCustomer), HttpStatusCode.UnprocessableEntity, "deal.contact_not_in_company");

        await admin.PatchAsJsonAsync($"/api/companies/{companyId}", new { isActive = false });
        await AssertProblemAsync(await PostDealAsync(admin, companyId), HttpStatusCode.UnprocessableEntity, "deal.company_inactive");
    }

    [Fact]
    public async Task Deal_MovesWinsReopensAndLoses_AndHistoryRecordsIt()
    {
        var admin = await AsAdminAsync();
        var deal = await CreateDealAsync(admin, await CreateCompanyAsync(admin));
        var id = deal.GetProperty("id").GetInt32();
        var lastStage = (await GetStagesAsync(admin))[^1].GetProperty("id").GetInt32();

        var moved = await ReadJsonAsync(await admin.PostAsJsonAsync($"/api/deals/{id}/move", new { stageId = lastStage }));
        Assert.Equal(lastStage, moved.GetProperty("stageId").GetInt32());

        var won = await ReadJsonAsync(await admin.PostAsync($"/api/deals/{id}/win", null));
        Assert.Equal("won", won.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, won.GetProperty("closedAt").ValueKind);

        await AssertProblemAsync(await admin.PostAsJsonAsync($"/api/deals/{id}/move", new { stageId = lastStage }),
            HttpStatusCode.UnprocessableEntity, "deal.closed");

        var reopened = await ReadJsonAsync(await admin.PostAsync($"/api/deals/{id}/reopen", null));
        Assert.Equal("open", reopened.GetProperty("status").GetString());
        await AssertProblemAsync(await admin.PostAsync($"/api/deals/{id}/reopen", null), HttpStatusCode.UnprocessableEntity, "deal.already_open");

        var lost = await ReadJsonAsync(await admin.PostAsJsonAsync($"/api/deals/{id}/lose", new { reason = "Too expensive" }));
        Assert.Equal("lost", lost.GetProperty("status").GetString());
        Assert.Equal("Too expensive", lost.GetProperty("lostReason").GetString());

        var history = await ReadJsonAsync(await admin.GetAsync($"/api/deals/{id}/history"));
        Assert.Contains(history.GetProperty("items").EnumerateArray(), e => e.GetProperty("changes").TryGetProperty("status", out _));
    }

    [Fact]
    public async Task UpdateDeal_ReplacesFields_AndCanClearTheContact()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var deal = await CreateDealAsync(admin, companyId, await CreateCustomerAsync(admin, companyId));
        var id = deal.GetProperty("id").GetInt32();

        var updated = await ReadJsonAsync(await admin.PutAsJsonAsync($"/api/deals/{id}", new
        {
            title = "Renamed", companyId, customerId = (int?)null, amount = 99.99m, currency = "EUR", expectedCloseDate = "2030-01-31"
        }));

        Assert.Equal("Renamed", updated.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("customerId").ValueKind);
        Assert.Equal("EUR", updated.GetProperty("currency").GetString());
        Assert.Equal("2030-01-31", updated.GetProperty("expectedCloseDate").GetString());
    }

    [Fact]
    public async Task ListDeals_FiltersByCompanyStatusAndSearch()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var marker = Unique("Find");
        await CreateDealAsync(admin, companyId, title: $"{marker} one");
        var won = await CreateDealAsync(admin, companyId, title: $"{marker} two");
        await admin.PostAsync($"/api/deals/{won.GetProperty("id").GetInt32()}/win", null);

        var all = await ReadJsonAsync(await admin.GetAsync($"/api/deals?companyId={companyId}"));
        Assert.Equal(2, all.GetProperty("totalCount").GetInt32());

        var open = await ReadJsonAsync(await admin.GetAsync($"/api/deals?companyId={companyId}&status=open"));
        Assert.Equal($"{marker} one", Assert.Single(open.GetProperty("items").EnumerateArray()).GetProperty("title").GetString());

        var search = await ReadJsonAsync(await admin.GetAsync($"/api/deals?search={marker.ToLowerInvariant()}&sortBy=title&sortDirection=desc"));
        Assert.Equal([$"{marker} two", $"{marker} one"], search.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Board_GroupsOpenDealsByStage_WithTotalsPerCurrency()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var stages = await GetStagesAsync(admin);
        var stage = stages[0];
        var probability = stage.GetProperty("probability").GetInt32();

        await CreateDealAsync(admin, companyId, amount: 100m, currency: "TRY");
        await CreateDealAsync(admin, companyId, amount: 50m, currency: "TRY");
        await CreateDealAsync(admin, companyId, amount: 10m, currency: "EUR");
        var closed = await CreateDealAsync(admin, companyId, amount: 1000m, currency: "TRY");
        await admin.PostAsync($"/api/deals/{closed.GetProperty("id").GetInt32()}/win", null);

        var board = await ReadJsonAsync(await admin.GetAsync($"/api/deals/board?companyId={companyId}"));
        Assert.Equal(stages.Count, board.GetArrayLength());

        var column = board[0];
        Assert.Equal(3, column.GetProperty("deals").GetArrayLength());
        var tryTotal = column.GetProperty("totals").EnumerateArray().Single(t => t.GetProperty("currency").GetString() == "TRY");
        Assert.Equal(150m, tryTotal.GetProperty("amount").GetDecimal());
        Assert.Equal(150m * probability / 100m, tryTotal.GetProperty("weightedAmount").GetDecimal());
    }

    [Fact]
    public async Task Summary_CountsOpenAndWonThisMonth()
    {
        var admin = await AsAdminAsync();
        var before = await ReadJsonAsync(await admin.GetAsync("/api/deals/summary"));

        var companyId = await CreateCompanyAsync(admin);
        await CreateDealAsync(admin, companyId, amount: 70m, currency: "USD");
        var won = await CreateDealAsync(admin, companyId, amount: 30m, currency: "USD");
        await admin.PostAsync($"/api/deals/{won.GetProperty("id").GetInt32()}/win", null);
        var lost = await CreateDealAsync(admin, companyId, amount: 5m, currency: "USD");
        await admin.PostAsJsonAsync($"/api/deals/{lost.GetProperty("id").GetInt32()}/lose", new { reason = (string?)null });

        var after = await ReadJsonAsync(await admin.GetAsync("/api/deals/summary"));

        Assert.Equal(70m, UsdAmount(after, "open") - UsdAmount(before, "open"));
        Assert.Equal(30m, UsdAmount(after, "wonThisMonth") - UsdAmount(before, "wonThisMonth"));
        Assert.Equal(1, after.GetProperty("lostThisMonth").GetInt32() - before.GetProperty("lostThisMonth").GetInt32());

        static decimal UsdAmount(JsonElement summary, string list) => summary.GetProperty(list).EnumerateArray()
            .Where(t => t.GetProperty("currency").GetString() == "USD")
            .Select(t => t.GetProperty("amount").GetDecimal())
            .SingleOrDefault();
    }

    [Fact]
    public async Task CompanyOrContactWithDeals_CannotBeDeleted()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var customerId = await CreateCustomerAsync(admin, companyId);
        var deal = await CreateDealAsync(admin, companyId, customerId);

        await AssertProblemAsync(await admin.DeleteAsync($"/api/customers/{customerId}"), HttpStatusCode.Conflict, "customer.has_deals");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/deals/{deal.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/customers/{customerId}")).StatusCode);

        await CreateDealAsync(admin, companyId);
        await AssertProblemAsync(await admin.DeleteAsync($"/api/companies/{companyId}"), HttpStatusCode.Conflict, "company.has_deals");
    }

    // ---------------------------------------------------------------- Helpers

    private static async Task<List<JsonElement>> GetStagesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/pipeline-stages", Json)).EnumerateArray().ToList();

    private static async Task<int> CreateCustomerAsync(HttpClient client, int companyId)
    {
        var response = await client.PostAsJsonAsync("/api/customers",
            new { firstName = "Ada", lastName = "Lovelace", email = $"{Guid.NewGuid():N}@test.local", companyId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> PostDealAsync(HttpClient client, int companyId, int? customerId = null) =>
        client.PostAsJsonAsync("/api/deals", new { title = "Deal", companyId, customerId, amount = 1m, currency = "TRY" });

    private static async Task<JsonElement> CreateDealAsync(
        HttpClient client, int companyId, int? customerId = null, string? title = null,
        decimal amount = 1000m, string currency = "TRY", int? stageId = null)
    {
        var response = await client.PostAsJsonAsync("/api/deals",
            new { title = title ?? Unique("Deal"), companyId, customerId, amount, currency, stageId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }
}
