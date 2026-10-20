using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.BuildingBlocks.Messaging;
using CRM.IntegrationTests.Infrastructure;
using CRM.Modules.Tasks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.IntegrationTests;

public class NotificationsTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task AssigningATask_NotifiesTheAssignee_WithTheActorsName()
    {
        var admin = await AsAdminAsync();
        var adminName = await NameAsync(admin);
        var (user, userId) = await NewUserAsync(admin);
        var title = Unique("Prepare offer");

        await admin.PostAsJsonAsync("/api/tasks", new { title, priority = "normal", assigneeId = userId });

        var notification = await WaitForNotificationAsync(user, n => n.GetProperty("subject").GetString() == title);
        Assert.Equal("task.assigned", notification.GetProperty("type").GetString());
        Assert.Equal(adminName, notification.GetProperty("actorName").GetString());
        Assert.Equal("/tasks", notification.GetProperty("link").GetString());
        Assert.Equal(JsonValueKind.Null, notification.GetProperty("readAt").ValueKind);
    }

    [Fact]
    public async Task CompletingSomeoneElsesTask_NotifiesTheCreator_ButYourOwnActionsDoNot()
    {
        var admin = await AsAdminAsync();
        var (user, userId) = await NewUserAsync(admin);
        var (creator, creatorId) = await NewUserAsync(admin);

        // The creator assigns a task to the user, who completes it.
        var title = Unique("Send contract");
        var task = await ReadJsonAsync(await creator.PostAsJsonAsync("/api/tasks", new { title, priority = "normal", assigneeId = userId }));
        await user.PostAsync($"/api/tasks/{task.GetProperty("id").GetInt32()}/complete", null);

        var done = await WaitForNotificationAsync(creator, n => n.GetProperty("type").GetString() == "task.completed");
        Assert.Equal(title, done.GetProperty("subject").GetString());

        // A task the creator made for themselves and completed: nothing new.
        var own = await ReadJsonAsync(await creator.PostAsJsonAsync("/api/tasks", new { title = Unique("Own"), priority = "normal" }));
        await creator.PostAsync($"/api/tasks/{own.GetProperty("id").GetInt32()}/complete", null);
        await Task.Delay(1000);
        Assert.Equal(1, (await ReadJsonAsync(await creator.GetAsync("/api/notifications"))).GetProperty("totalCount").GetInt32());
        Assert.NotEqual(creatorId, userId);
    }

    [Fact]
    public async Task WinningSomeoneElsesDeal_NotifiesTheDealsCreator()
    {
        var admin = await AsAdminAsync();
        var (creator, _) = await NewUserAsync(admin);
        var companyId = await CreateCompanyAsync(creator);
        var title = Unique("Big deal");
        var deal = await ReadJsonAsync(await creator.PostAsJsonAsync("/api/deals", new { title, companyId, amount = 10m, currency = "EUR" }));
        var dealId = deal.GetProperty("id").GetInt32();

        await admin.PostAsync($"/api/deals/{dealId}/win", null);

        var won = await WaitForNotificationAsync(creator, n => n.GetProperty("type").GetString() == "deal.won");
        Assert.Equal(title, won.GetProperty("subject").GetString());
        Assert.Equal($"/deals/{dealId}", won.GetProperty("link").GetString());
    }

    [Fact]
    public async Task UnreadCount_MarkRead_AndMarkAllRead_OnlyTouchYourOwn()
    {
        var admin = await AsAdminAsync();
        var (user, userId) = await NewUserAsync(admin);
        var (other, _) = await NewUserAsync(admin);

        await admin.PostAsJsonAsync("/api/tasks", new { title = Unique("One"), priority = "normal", assigneeId = userId });
        await admin.PostAsJsonAsync("/api/tasks", new { title = Unique("Two"), priority = "normal", assigneeId = userId });
        await WaitForAsync(async () => await UnreadAsync(user) == 2);

        var first = (await ReadJsonAsync(await user.GetAsync("/api/notifications"))).GetProperty("items")[0].GetProperty("id").GetInt32();

        await AssertProblemAsync(await other.PostAsync($"/api/notifications/{first}/read", null), HttpStatusCode.NotFound, "notification.not_found");

        Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync($"/api/notifications/{first}/read", null)).StatusCode);
        Assert.Equal(1, await UnreadAsync(user));
        var unreadOnly = await ReadJsonAsync(await user.GetAsync("/api/notifications?unreadOnly=true"));
        Assert.Equal(1, unreadOnly.GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, await UnreadAsync(user));
    }

    [Fact]
    public async Task AnEventDeliveredTwice_CreatesOneNotification()
    {
        var admin = await AsAdminAsync();
        var (user, userId) = await NewUserAsync(admin);
        var assigned = new TaskAssigned(TaskId: 1, AssigneeId: userId, AssignedBy: null, Title: Unique("Twice"));

        for(var i = 0; i < 2; i++)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            foreach(var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<TaskAssigned>>())
            {
                await handler.HandleAsync(assigned, CancellationToken.None);
            }
        }

        var page = await ReadJsonAsync(await user.GetAsync("/api/notifications"));
        Assert.Single(page.GetProperty("items").EnumerateArray(), n => n.GetProperty("subject").GetString() == assigned.Title);
    }

    [Fact]
    public async Task Anonymous_CannotReadNotifications()
    {
        await AssertProblemAsync(await Anonymous().GetAsync("/api/notifications/unread-count"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<(HttpClient Client, int Id)> NewUserAsync(HttpClient admin)
    {
        var (email, password) = await RegisterUserAsync();
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/users?search={Uri.EscapeDataString(email)}", Json);
        return (await AsUserAsync(email, password), page.GetProperty("items")[0].GetProperty("id").GetInt32());
    }

    private static async Task<string> NameAsync(HttpClient client)
    {
        var me = await ReadJsonAsync(await client.GetAsync("/api/auth/me"));
        return $"{me.GetProperty("firstName").GetString()} {me.GetProperty("lastName").GetString()}";
    }

    private static async Task<int> UnreadAsync(HttpClient client) =>
        (await ReadJsonAsync(await client.GetAsync("/api/notifications/unread-count"))).GetProperty("count").GetInt32();

    /// <summary>Notifications arrive through the outbox, a moment after the change.</summary>
    private static async Task<JsonElement> WaitForNotificationAsync(HttpClient client, Func<JsonElement, bool> match)
    {
        JsonElement? found = null;
        await WaitForAsync(async () =>
        {
            var page = await ReadJsonAsync(await client.GetAsync("/api/notifications"));
            found = page.GetProperty("items").EnumerateArray().Cast<JsonElement?>().FirstOrDefault(n => match(n!.Value));
            return found is not null;
        });
        return found!.Value;
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while(DateTime.UtcNow < deadline)
        {
            if(await condition()) return;
            await Task.Delay(200);
        }

        Assert.Fail("Timed out waiting for the notification.");
    }
}
