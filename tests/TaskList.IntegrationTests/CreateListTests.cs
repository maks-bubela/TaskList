using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskList.Infrastructure.MongoDB;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class CreateListTests : IClassFixture<MongoFixture>
{
    private readonly MongoFixture _mongo;

    public CreateListTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task PostPersistsNormalizedListThatOwnerCanReadAtLocation()
    {
        await using var factory = new TaskListApiFactory(_mongo.ConnectionString);
        using var rootClient = factory.CreateClient();
        await AssertCreatedListAsync(factory, rootClient, "");

        const string pathBase = "/tasklist";
        factory.Server.BaseAddress = new Uri($"http://localhost{pathBase}/");
        using var prefixedClient = factory.Server.CreateClient();
        await AssertCreatedListAsync(factory, prefixedClient, pathBase);
    }

    private static async Task AssertCreatedListAsync(TaskListApiFactory factory, HttpClient client, string pathBase)
    {
        var callerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add(HttpConstants.CallerIdHeader, callerId.ToString());

        var collectionPath = pathBase + HttpConstants.TaskListsPath;
        using var response = await client.PostAsJsonAsync(collectionPath, new { name = "  Shopping  list  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var payload = body.RootElement;
        var id = payload.GetProperty(JsonPropertyConstants.Id).GetGuid();
        var createdAtUtc = payload.GetProperty(JsonPropertyConstants.CreatedAtUtc).GetDateTime();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(callerId, payload.GetProperty(JsonPropertyConstants.OwnerId).GetGuid());
        Assert.Equal("Shopping  list", payload.GetProperty(JsonPropertyConstants.Name).GetString());
        Assert.Equal(DateTimeKind.Utc, createdAtUtc.Kind);
        Assert.Equal($"{collectionPath}/{id}", response.Headers.Location?.OriginalString);

        var collection = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var saved = await collection.Find(list => list.Id == id).SingleAsync();
        Assert.Equal(callerId, saved.OwnerId);
        Assert.Equal("Shopping  list", saved.Name);
        Assert.Equal(createdAtUtc, saved.CreatedAtUtc);
        Assert.Empty(saved.LinkedUserIds);

        using var read = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var readList = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, readList.EnumerateObject().Count());
        Assert.Equal(id, readList.GetProperty(JsonPropertyConstants.Id).GetGuid());
        Assert.Equal("Shopping  list", readList.GetProperty(JsonPropertyConstants.Name).GetString());
        Assert.Equal(callerId, readList.GetProperty(JsonPropertyConstants.OwnerId).GetGuid());
        Assert.Equal(createdAtUtc, readList.GetProperty(JsonPropertyConstants.CreatedAtUtc).GetDateTime());
    }

    [Fact]
    public async Task PostRejectsBlankNameWithProblemDetailsAndDoesNotWrite()
    {
        await using var factory = new TaskListApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(HttpConstants.CallerIdHeader, Guid.NewGuid().ToString());

        using var response = await client.PostAsJsonAsync(HttpConstants.TaskListsPath, new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        var problem = Assert.IsType<ProblemDetails>(await response.Content.ReadFromJsonAsync<ProblemDetails>());
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        Assert.NotNull(problem.Type);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("The name must contain 1 to 255 characters after trimming.", problem.Detail);
        Assert.False(problem.Extensions.ContainsKey("errors"));
        var collection = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        Assert.Equal(0L, await collection.CountDocumentsAsync(FilterDefinition<TaskListDocument>.Empty));
    }
}
