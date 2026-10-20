using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.IntegrationTests.Infrastructure;
using CRM.Modules.Tasks.Contracts;

namespace CRM.IntegrationTests;

public class TasksTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task CreateTask_DefaultsToTheCaller_AndFillsNames()
    {
        var admin = await AsAdminAsync();
        var me = await ReadJsonAsync(await admin.GetAsync("/api/auth/me"));
        var companyName = Unique("Acme");
        var companyId = await CreateCompanyAsync(admin, companyName);

        var task = await CreateTaskAsync(admin, "Send proposal", due: Today, companyId: companyId);

        Assert.Equal(me.GetProperty("id").GetInt32(), task.GetProperty("assigneeId").GetInt32());
        Assert.Equal(task.GetProperty("createdByName").GetString(), task.GetProperty("assigneeName").GetString());
        Assert.Equal(companyName, task.GetProperty("companyName").GetString());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), task.GetProperty("dueDate").GetString());
        Assert.False(task.GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task CreateTask_InvalidInput_ReturnsFieldErrors()
    {
        var admin = await AsAdminAsync();

        var problem = await AssertProblemAsync(
            await admin.PostAsJsonAsync("/api/tasks", new { title = "", priority = "urgent", customerId = 1 }),
            HttpStatusCode.BadRequest, "validation_failed");

        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("priority", out _));
        Assert.True(errors.TryGetProperty("companyId", out _));
    }

    [Fact]
    public async Task Assigning_ToAnotherUser_PublishesTaskAssigned_AndInactiveUsersAreRefused()
    {
        var admin = await AsAdminAsync();
        var (email, _) = await RegisterUserAsync();
        var userId = await GetUserIdAsync(admin, email);

        var task = await CreateTaskAsync(admin, "For someone else", assigneeId: userId);
        var taskId = task.GetProperty("id").GetInt32();
        Assert.Equal(userId, task.GetProperty("assigneeId").GetInt32());

        await WaitForAsync(() => Factory.DeliveredEvents.OfType<TaskAssigned>().Any(e => e.TaskId == taskId && e.AssigneeId == userId));

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{userId}", new { isActive = false })).StatusCode);
        await AssertProblemAsync(await admin.PostAsJsonAsync("/api/tasks", new { title = "x", priority = "normal", assigneeId = userId }),
            HttpStatusCode.UnprocessableEntity, "task.assignee_inactive");
        await AssertProblemAsync(await admin.PostAsJsonAsync("/api/tasks", new { title = "x", priority = "normal", assigneeId = 999_999 }),
            HttpStatusCode.NotFound, "user.not_found");

        // The existing task keeps its (now inactive) assignee when edited.
        var edited = await admin.PutAsJsonAsync($"/api/tasks/{taskId}", new { title = "Renamed", priority = "low", assigneeId = userId });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }

    [Fact]
    public async Task OnlyAssigneeCreatorOrManager_CanChangeATask()
    {
        var admin = await AsAdminAsync();
        var (assigneeEmail, assigneePassword) = await RegisterUserAsync();
        var (otherEmail, otherPassword) = await RegisterUserAsync();
        var assignee = await AsUserAsync(assigneeEmail, assigneePassword);
        var other = await AsUserAsync(otherEmail, otherPassword);

        // Created by the admin for the assignee.
        var task = await CreateTaskAsync(admin, "Shared work", assigneeId: await GetUserIdAsync(admin, assigneeEmail));
        var id = task.GetProperty("id").GetInt32();

        await AssertProblemAsync(await other.PostAsync($"/api/tasks/{id}/complete", null), HttpStatusCode.Forbidden, "task.not_yours");
        await AssertProblemAsync(await other.DeleteAsync($"/api/tasks/{id}"), HttpStatusCode.Forbidden, "task.not_yours");

        var done = await ReadJsonAsync(await assignee.PostAsync($"/api/tasks/{id}/complete", null));
        Assert.True(done.GetProperty("isDone").GetBoolean());
        await AssertProblemAsync(await assignee.PostAsync($"/api/tasks/{id}/complete", null), HttpStatusCode.UnprocessableEntity, "task.already_done");

        var reopened = await ReadJsonAsync(await assignee.PostAsync($"/api/tasks/{id}/reopen", null));
        Assert.False(reopened.GetProperty("isDone").GetBoolean());

        // Users can see the assignee list without identity.users.read.
        var assignees = await ReadJsonAsync(await other.GetAsync("/api/tasks/assignees"));
        Assert.Contains(assignees.EnumerateArray(), a => a.GetProperty("id").GetInt32() == task.GetProperty("assigneeId").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task MyTasks_AreListedInWorkOrder_AndSummarized()
    {
        var (email, password) = await RegisterUserAsync();
        var user = await AsUserAsync(email, password);

        await CreateTaskAsync(user, "no date");
        await CreateTaskAsync(user, "overdue", due: Today.AddDays(-2), priority: "low");
        await CreateTaskAsync(user, "today", due: Today, priority: "high");
        var done = await CreateTaskAsync(user, "done", due: Today.AddDays(-5));
        await user.PostAsync($"/api/tasks/{done.GetProperty("id").GetInt32()}/complete", null);

        var mine = await ReadJsonAsync(await user.GetAsync("/api/tasks?mine=true"));
        Assert.Equal(["overdue", "today", "no date", "done"], Titles(mine));

        var dueByToday = await ReadJsonAsync(await user.GetAsync($"/api/tasks?mine=true&status=open&dueTo={Today:yyyy-MM-dd}"));
        Assert.Equal(["overdue", "today"], Titles(dueByToday));

        var summary = await ReadJsonAsync(await user.GetAsync($"/api/tasks/my-summary?today={Today:yyyy-MM-dd}"));
        Assert.Equal(3, summary.GetProperty("open").GetInt32());
        Assert.Equal(1, summary.GetProperty("overdue").GetInt32());
        Assert.Equal(1, summary.GetProperty("dueToday").GetInt32());

        static IEnumerable<string?> Titles(JsonElement page) =>
            page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("title").GetString());
    }

    [Fact]
    public async Task LinksMustMatch_AndOpenTasksBlockDeletes()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var otherCompanyId = await CreateCompanyAsync(admin);
        var otherCustomer = await CreateCustomerAsync(admin, otherCompanyId);

        await AssertProblemAsync(await admin.PostAsJsonAsync("/api/tasks", new { title = "x", priority = "normal", companyId, customerId = otherCustomer }),
            HttpStatusCode.UnprocessableEntity, "task.contact_not_in_company");

        var customerId = await CreateCustomerAsync(admin, companyId);
        var task = await CreateTaskAsync(admin, "Follow up", companyId: companyId, customerId: customerId);
        var id = task.GetProperty("id").GetInt32();

        await AssertProblemAsync(await admin.DeleteAsync($"/api/customers/{customerId}"), HttpStatusCode.Conflict, "customer.has_open_tasks");

        // Done tasks don't block.
        await admin.PostAsync($"/api/tasks/{id}/complete", null);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/customers/{customerId}")).StatusCode);
    }

    [Fact]
    public async Task UserWithOpenTasks_CannotBeDeleted()
    {
        var admin = await AsAdminAsync();
        var (email, _) = await RegisterUserAsync();
        var userId = await GetUserIdAsync(admin, email);

        var task = await CreateTaskAsync(admin, "Theirs", assigneeId: userId);
        await AssertProblemAsync(await admin.DeleteAsync($"/api/users/{userId}"), HttpStatusCode.Conflict, "user.has_open_tasks");

        await admin.DeleteAsync($"/api/tasks/{task.GetProperty("id").GetInt32()}");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/users/{userId}")).StatusCode);
    }

    // ---------------------------------------------------------------- Helpers

    private static async Task<JsonElement> CreateTaskAsync(
        HttpClient client, string title, DateOnly? due = null, string priority = "normal", int? assigneeId = null,
        int? companyId = null, int? customerId = null)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new
        {
            title,
            dueDate = due?.ToString("yyyy-MM-dd"),
            priority,
            assigneeId,
            companyId,
            customerId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<int> GetUserIdAsync(HttpClient admin, string email)
    {
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/users?search={Uri.EscapeDataString(email)}", Json);
        return page.GetProperty("items")[0].GetProperty("id").GetInt32();
    }

    private static async Task<int> CreateCustomerAsync(HttpClient client, int companyId)
    {
        var response = await client.PostAsJsonAsync("/api/customers",
            new { firstName = "Ada", lastName = "Lovelace", email = $"{Guid.NewGuid():N}@test.local", companyId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while(DateTime.UtcNow < deadline && !condition())
        {
            await Task.Delay(100);
        }

        Assert.True(condition());
    }
}
