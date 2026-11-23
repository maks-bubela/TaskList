using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskList.Infrastructure.MongoDB;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class ListMutationTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task OwnerAndLinkedUserCanRenameButOutsiderCannot()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var list = NewList();
        var other = NewList(list.OwnerId);
        await lists.InsertManyAsync([list, other]);

        using var ownerRename = await SendAsync(client, HttpMethod.Patch, list.Id, list.OwnerId, "  Owner  name  ");
        await AssertNoContentAsync(ownerRename);
        AssertDocument(list, await ReadAsync(lists, list.Id), "Owner  name");

        using var linkedRename = await SendAsync(client, HttpMethod.Patch, list.Id, list.LinkedUserIds[0], "Linked name");
        await AssertNoContentAsync(linkedRename);
        AssertDocument(list, await ReadAsync(lists, list.Id), "Linked name");

        using var forbidden = await SendAsync(client, HttpMethod.Patch, list.Id, Guid.NewGuid(), "Forbidden name");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        AssertDocument(list, await ReadAsync(lists, list.Id), "Linked name");
        AssertDocument(other, await ReadAsync(lists, other.Id), other.Name);
    }

    [Fact]
    public async Task OnlyOwnerCanDeleteListAndItCanNoLongerBeRead()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var list = NewList();
        var other = NewList(list.OwnerId);
        await lists.InsertManyAsync([list, other]);

        using var forbidden = await SendAsync(client, HttpMethod.Delete, list.Id, list.LinkedUserIds[0]);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        AssertDocument(list, await ReadAsync(lists, list.Id), list.Name);

        using var deleted = await SendAsync(client, HttpMethod.Delete, list.Id, list.OwnerId);
        await AssertNoContentAsync(deleted);
        Assert.Null(await ReadAsync(lists, list.Id));
        AssertDocument(other, await ReadAsync(lists, other.Id), other.Name);

        using var read = await SendAsync(client, HttpMethod.Get, list.Id, list.OwnerId);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    private static TaskListDocument NewList(Guid? ownerId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Original name",
        OwnerId = ownerId ?? Guid.NewGuid(),
        CreatedAtUtc = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc),
        LinkedUserIds = [Guid.NewGuid()]
    };

    private static async Task<TaskListDocument?> ReadAsync(IMongoCollection<TaskListDocument> lists, Guid id) =>
        await lists.Find(list => list.Id == id).SingleOrDefaultAsync();

    private static void AssertDocument(TaskListDocument expected, TaskListDocument? actual, string expectedName)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal(expected.OwnerId, actual.OwnerId);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.LinkedUserIds, actual.LinkedUserIds);
    }

    private static async Task AssertNoContentAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, Guid listId, Guid callerId, string? name = null)
    {
        using var request = new HttpRequestMessage(method, $"{HttpConstants.TaskListsPath}/{listId}");
        request.Headers.Add(HttpConstants.CallerIdHeader, callerId.ToString());
        if (method == HttpMethod.Patch)
        {
            request.Content = JsonContent.Create(new { name });
        }

        return await client.SendAsync(request);
    }
}
