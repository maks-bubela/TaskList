using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskList.Infrastructure.MongoDB;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class ListPageTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    private static readonly DateTime Timestamp = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PagesUseOneAccessFilterAndOrderByDateThenId()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var caller = Guid.NewGuid();
        var other = Guid.NewGuid();
        var newest = NewList(1, caller, Timestamp.AddDays(1));
        var linked = NewList(4, other, Timestamp, caller);
        var owned = NewList(3, caller, Timestamp);
        var oldest = NewList(6, other, Timestamp.AddDays(-1), caller);
        var hidden = NewList(5, other, Timestamp.AddDays(2));
        await Collection(factory).InsertManyAsync([linked, hidden, oldest, owned, newest]);

        client.DefaultRequestHeaders.Add(HttpConstants.CallerIdHeader, caller.ToString());
        await AssertPageAsync(client, 1, 4, newest, linked);
        await AssertPageAsync(client, 2, 4, owned, oldest);
        await AssertPageAsync(client, int.MaxValue, 4);
    }

    private static async Task AssertPageAsync(HttpClient client, int page, long totalCount, params TaskListDocument[] expected)
    {
        const int pageSize = 2;
        using var response = await client.GetAsync($"{HttpConstants.TaskListsPath}?page={page}&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, body.EnumerateObject().Count());
        Assert.Equal(page, body.GetProperty(JsonPropertyConstants.Page).GetInt32());
        Assert.Equal(pageSize, body.GetProperty(JsonPropertyConstants.PageSize).GetInt32());
        Assert.Equal(totalCount, body.GetProperty(JsonPropertyConstants.TotalCount).GetInt64());

        var items = body.GetProperty(JsonPropertyConstants.Items).EnumerateArray().ToArray();
        Assert.Equal(expected.Select(list => list.Id), items.Select(item => item.GetProperty(JsonPropertyConstants.Id).GetGuid()));
        Assert.Equal(expected.Select(list => list.Name), items.Select(item => item.GetProperty(JsonPropertyConstants.Name).GetString()));
        Assert.All(items, item => Assert.Equal(2, item.EnumerateObject().Count()));
    }

    private static IMongoCollection<TaskListDocument> Collection(TaskListApiFactory factory) =>
        factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();

    private static TaskListDocument NewList(int id, Guid owner, DateTime created, params Guid[] linked) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),
        Name = $"List {id}",
        OwnerId = owner,
        CreatedAtUtc = created,
        LinkedUserIds = linked
    };
}
