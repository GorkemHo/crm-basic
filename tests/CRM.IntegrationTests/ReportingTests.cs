using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.IntegrationTests.Infrastructure;

namespace CRM.IntegrationTests;

public class ReportingTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Reports_NeedTheReportingPermission()
    {
        var (email, password) = await RegisterUserAsync();
        var user = await AsUserAsync(email, password);

        foreach(var report in new[] { "sales", "pipeline", "activities", "tasks" })
        {
            await AssertProblemAsync(await user.GetAsync($"/api/reports/{report}"), HttpStatusCode.Forbidden, "forbidden");
            Assert.Equal(HttpStatusCode.OK, (await (await AsAdminAsync()).GetAsync($"/api/reports/{report}")).StatusCode);
        }
    }

    [Fact]
    public async Task Period_IsValidated_AndDefaultsToSixMonths()
    {
        var admin = await AsAdminAsync();

        var reversed = await AssertProblemAsync(await admin.GetAsync("/api/reports/sales?from=2026-05-01&to=2026-04-01"),
            HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(reversed.GetProperty("errors").TryGetProperty("to", out _));

        var tooLong = await AssertProblemAsync(await admin.GetAsync("/api/reports/sales?from=2020-01-01&to=2026-01-01"),
            HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(tooLong.GetProperty("errors").TryGetProperty("to", out _));

        var report = await ReadJsonAsync(await admin.GetAsync("/api/reports/sales"));
        Assert.Equal(6, report.GetProperty("months").GetArrayLength());
        var last = report.GetProperty("months")[5];
        Assert.Equal(Today.Year, last.GetProperty("year").GetInt32());
        Assert.Equal(Today.Month, last.GetProperty("month").GetInt32());
    }

    [Fact]
    public async Task SalesReport_CountsDealsClosedInThePeriod_PerCurrency()
    {
        var admin = await AsAdminAsync();
        var url = $"/api/reports/sales?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var before = await ReadJsonAsync(await admin.GetAsync(url));

        var companyId = await CreateCompanyAsync(admin);
        await CloseDealAsync(admin, companyId, 1_000m, "EUR", won: true);
        await CloseDealAsync(admin, companyId, 500m, "EUR", won: true);
        await CloseDealAsync(admin, companyId, 70m, "EUR", won: false, reason: "Reporting test reason");

        var after = await ReadJsonAsync(await admin.GetAsync(url));

        Assert.Equal(2, after.GetProperty("wonCount").GetInt32() - before.GetProperty("wonCount").GetInt32());
        Assert.Equal(1, after.GetProperty("lostCount").GetInt32() - before.GetProperty("lostCount").GetInt32());
        Assert.Equal(1_500m, Eur(after) - Eur(before));
        Assert.Contains(after.GetProperty("lostReasons").EnumerateArray(), r => r.GetProperty("reason").GetString() == "Reporting test reason");
        Assert.InRange(after.GetProperty("winRate").GetDouble(), 0, 1);

        static decimal Eur(JsonElement report) => report.GetProperty("won").EnumerateArray()
            .Where(c => c.GetProperty("currency").GetString() == "EUR").Select(c => c.GetProperty("amount").GetDecimal()).SingleOrDefault();
    }

    [Fact]
    public async Task PipelineReport_ShowsEveryStage_WithWeightedTotals()
    {
        var admin = await AsAdminAsync();
        var stages = await admin.GetFromJsonAsync<JsonElement>("/api/pipeline-stages", Json);

        var report = await ReadJsonAsync(await admin.GetAsync("/api/reports/pipeline"));

        Assert.Equal(stages.GetArrayLength(), report.GetArrayLength());
        foreach(var stage in report.EnumerateArray())
        {
            var probability = stage.GetProperty("probability").GetInt32();
            foreach(var total in stage.GetProperty("totals").EnumerateArray())
            {
                var expected = Math.Round(total.GetProperty("amount").GetDecimal() * probability / 100m, 2, MidpointRounding.AwayFromZero);
                Assert.Equal(expected, total.GetProperty("weightedAmount").GetDecimal());
            }
        }
    }

    [Fact]
    public async Task ActivityAndTaskReports_ArePerPerson_WithNames()
    {
        var admin = await AsAdminAsync();
        var (email, password) = await RegisterUserAsync();
        var user = await AsUserAsync(email, password);
        var userId = (await ReadJsonAsync(await user.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();
        var companyId = await CreateCompanyAsync(user);

        foreach(var type in new[] { "call", "call", "note" })
        {
            Assert.Equal(HttpStatusCode.Created, (await user.PostAsJsonAsync("/api/activities",
                new { type, subject = "Report me", occurredAt = DateTime.UtcNow, companyId })).StatusCode);
        }

        await user.PostAsJsonAsync("/api/tasks", new { title = "Late", priority = "normal", dueDate = Today.AddDays(-1).ToString("yyyy-MM-dd") });
        await user.PostAsJsonAsync("/api/tasks", new { title = "Later", priority = "normal" });
        var done = await ReadJsonAsync(await user.PostAsJsonAsync("/api/tasks", new { title = "Done", priority = "normal" }));
        await user.PostAsync($"/api/tasks/{done.GetProperty("id").GetInt32()}/complete", null);

        var period = $"from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";

        var activities = await ReadJsonAsync(await admin.GetAsync($"/api/reports/activities?{period}"));
        var mine = activities.GetProperty("byUser").EnumerateArray().Single(u => u.GetProperty("userId").ValueKind == JsonValueKind.Number
                                                                                  && u.GetProperty("userId").GetInt32() == userId);
        Assert.Equal("Test User", mine.GetProperty("userName").GetString());
        Assert.Equal(3, mine.GetProperty("total").GetInt32());
        Assert.Equal(2, mine.GetProperty("types").EnumerateArray().Single(t => t.GetProperty("type").GetString() == "call").GetProperty("count").GetInt32());

        var tasks = await ReadJsonAsync(await admin.GetAsync($"/api/reports/tasks?{period}&today={Today:yyyy-MM-dd}"));
        var theirs = tasks.GetProperty("byAssignee").EnumerateArray().Single(a => a.GetProperty("assigneeId").GetInt32() == userId);
        Assert.Equal(2, theirs.GetProperty("open").GetInt32());
        Assert.Equal(1, theirs.GetProperty("overdue").GetInt32());
        Assert.Equal(1, theirs.GetProperty("completed").GetInt32());
    }

    private static async Task CloseDealAsync(HttpClient client, int companyId, decimal amount, string currency, bool won, string? reason = null)
    {
        var deal = await ReadJsonAsync(await client.PostAsJsonAsync("/api/deals", new { title = Unique("Report"), companyId, amount, currency }));
        var id = deal.GetProperty("id").GetInt32();
        var response = won
            ? await client.PostAsync($"/api/deals/{id}/win", null)
            : await client.PostAsJsonAsync($"/api/deals/{id}/lose", new { reason });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
