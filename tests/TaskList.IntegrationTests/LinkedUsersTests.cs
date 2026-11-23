using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskList.Infrastructure.MongoDB;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class LinkedUsersTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task OwnerAddsLinkOnceAndOnlyUsersWithAccessCanReadIt()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var list = NewList();
        var target = Guid.NewGuid();
        await lists.InsertOneAsync(list);
        var path = LinksPath(list.Id);
        var targetPath = $"{path}/{target}";

        await AssertStatusAsync(client, HttpMethod.Put, targetPath, list.OwnerId, HttpStatusCode.Created);
        await AssertStatusAsync(client, HttpMethod.Put, targetPath, list.OwnerId, HttpStatusCode.NoContent);
        await AssertLinksAsync(client, path, list.OwnerId, [target]);
        await AssertLinksAsync(client, path, target, [target]);
        await AssertStatusAsync(client, HttpMethod.Get, path, Guid.NewGuid(), HttpStatusCode.Forbidden);

        await AssertStoredLinksAsync(lists, list, [target]);
    }

    [Fact]
    public async Task LinkedUserCanGrantAccessToAnotherUser()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var linked = Guid.NewGuid();
        var target = Guid.NewGuid();
        var list = NewList(linked);
        await lists.InsertOneAsync(list);

        await AssertStatusAsync(client, HttpMethod.Put, $"{LinksPath(list.Id)}/{target}", linked, HttpStatusCode.Created);

        await AssertStoredLinksAsync(lists, list, [linked, target]);
    }

    [Fact]
    public async Task AddingOwnerReturnsConflictOnlyToCallersWithAccess()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var linked = Guid.NewGuid();
        var list = NewList(linked);
        await lists.InsertOneAsync(list);
        var ownerPath = $"{LinksPath(list.Id)}/{list.OwnerId}";

        await AssertStatusAsync(client, HttpMethod.Put, ownerPath, list.OwnerId, HttpStatusCode.Conflict);
        await AssertStatusAsync(client, HttpMethod.Put, ownerPath, linked, HttpStatusCode.Conflict);
        await AssertStatusAsync(client, HttpMethod.Put, ownerPath, Guid.NewGuid(), HttpStatusCode.Forbidden);

        await AssertStoredLinksAsync(lists, list, [linked]);
    }

    [Fact]
    public async Task LinkedUserCanRevokeAnotherUserAndGets404OnRepeat()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var linked = Guid.NewGuid();
        var target = Guid.NewGuid();
        var list = NewList(linked, target);
        await lists.InsertOneAsync(list);
        var targetPath = $"{LinksPath(list.Id)}/{target}";

        await AssertStatusAsync(client, HttpMethod.Delete, targetPath, linked, HttpStatusCode.NoContent);
        await AssertStatusAsync(client, HttpMethod.Delete, targetPath, linked, HttpStatusCode.NotFound);

        await AssertStoredLinksAsync(lists, list, [linked]);
    }

    [Fact]
    public async Task SelfRevocationReturnsForbiddenToFormerLinkedUserAndNotFoundToOwnerOnRetry()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var linked = Guid.NewGuid();
        var remaining = Guid.NewGuid();
        var list = NewList(linked, remaining);
        await lists.InsertOneAsync(list);
        var selfPath = $"{LinksPath(list.Id)}/{linked}";

        await AssertStatusAsync(client, HttpMethod.Delete, selfPath, linked, HttpStatusCode.NoContent);
        await AssertStatusAsync(client, HttpMethod.Delete, selfPath, linked, HttpStatusCode.Forbidden);
        await AssertStatusAsync(client, HttpMethod.Delete, selfPath, list.OwnerId, HttpStatusCode.NotFound);

        await AssertStoredLinksAsync(lists, list, [remaining]);
    }

    [Fact]
    public async Task RevokingOwnerReturnsConflictOnlyToCallersWithAccess()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var linked = Guid.NewGuid();
        var list = NewList(linked);
        await lists.InsertOneAsync(list);
        var ownerPath = $"{LinksPath(list.Id)}/{list.OwnerId}";

        await AssertStatusAsync(client, HttpMethod.Delete, ownerPath, list.OwnerId, HttpStatusCode.Conflict);
        await AssertStatusAsync(client, HttpMethod.Delete, ownerPath, linked, HttpStatusCode.Conflict);
        await AssertStatusAsync(client, HttpMethod.Delete, ownerPath, Guid.NewGuid(), HttpStatusCode.Forbidden);

        await AssertStoredLinksAsync(lists, list, [linked]);
    }

    private static TaskListDocument NewList(params Guid[] linkedUsers) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Original name",
        OwnerId = Guid.NewGuid(),
        CreatedAtUtc = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc),
        LinkedUserIds = linkedUsers
    };

    private static string LinksPath(Guid listId) => $"{HttpConstants.TaskListsPath}/{listId}/{HttpConstants.LinkedUsersSegment}";

    private static async Task AssertLinksAsync(HttpClient client, string path, Guid caller, Guid[] expected)
    {
        using var response = await SendAsync(client, HttpMethod.Get, path, caller);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<Guid[]>());
    }

    private static async Task AssertStatusAsync(
        HttpClient client, HttpMethod method, string path, Guid caller, HttpStatusCode expected)
    {
        using var response = await SendAsync(client, method, path, caller);
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task AssertStoredLinksAsync(
        IMongoCollection<TaskListDocument> lists, TaskListDocument original, Guid[] expected)
    {
        var saved = await lists.Find(list => list.Id == original.Id).SingleAsync();
        Assert.Equal(expected.Order(), saved.LinkedUserIds.Order());
        Assert.Equal(original.OwnerId, saved.OwnerId);
        Assert.Equal(original.Name, saved.Name);
        Assert.Equal(original.CreatedAtUtc, saved.CreatedAtUtc);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, Guid caller)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(HttpConstants.CallerIdHeader, caller.ToString());
        return await client.SendAsync(request);
    }
}
