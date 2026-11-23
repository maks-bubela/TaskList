using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskList.Infrastructure.MongoDB;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class GetListTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task LinkedUserCanReadListButUnrelatedCallerCannot()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var linkedUserId = Guid.NewGuid();
        var collection = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var list = new TaskListDocument
        {
            Id = Guid.NewGuid(),
            Name = "Shared list",
            OwnerId = Guid.NewGuid(),
            CreatedAtUtc = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc),
            LinkedUserIds = [linkedUserId]
        };
        await collection.InsertOneAsync(list);

        using var linkedResponse = await GetAsync(client, list.Id.ToString(), linkedUserId.ToString());
        using var unrelatedResponse = await GetAsync(client, list.Id.ToString(), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, linkedResponse.StatusCode);
        AssertList(await linkedResponse.Content.ReadFromJsonAsync<JsonElement>(),
            list.Id, list.Name, list.OwnerId, list.CreatedAtUtc);
        Assert.Equal(HttpStatusCode.Forbidden, unrelatedResponse.StatusCode);
    }

    [Fact]
    public async Task MalformedListIdReturnsModelBindingErrors()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        using var response = await GetAsync(client, "invalid-guid", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>());
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task MissingCallerHeaderReturnsBadRequest()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"{HttpConstants.TaskListsPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string listId, string callerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{HttpConstants.TaskListsPath}/{listId}");
        request.Headers.Add(HttpConstants.CallerIdHeader, callerId);

        return await client.SendAsync(request);
    }

    private static void AssertList(JsonElement body, Guid id, string name, Guid ownerId, DateTime createdAtUtc)
    {
        Assert.Equal(4, body.EnumerateObject().Count());
        Assert.Equal(id, body.GetProperty(JsonPropertyConstants.Id).GetGuid());
        Assert.Equal(name, body.GetProperty(JsonPropertyConstants.Name).GetString());
        Assert.Equal(ownerId, body.GetProperty(JsonPropertyConstants.OwnerId).GetGuid());
        Assert.Equal(createdAtUtc, body.GetProperty(JsonPropertyConstants.CreatedAtUtc).GetDateTime());
    }
}
