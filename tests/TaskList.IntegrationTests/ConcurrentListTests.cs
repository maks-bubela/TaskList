using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskList.Infrastructure.MongoDB;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class ConcurrentListTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task ConcurrentRevocationsReturn204And404AndPreserveOtherLinks()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        using var client = factory.CreateClient();
        var lists = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();
        var target = Guid.NewGuid();
        var remaining = Guid.NewGuid();
        var list = new TaskListDocument
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            OwnerId = Guid.NewGuid(),
            CreatedAtUtc = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc),
            LinkedUserIds = [target, remaining]
        };
        await lists.InsertOneAsync(list);
        var path = $"{HttpConstants.TaskListsPath}/{list.Id}/{HttpConstants.LinkedUsersSegment}/{target}";

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = RevokeAsync();
        var second = RevokeAsync();
        start.SetResult();
        var statuses = await Task.WhenAll(first, second);

        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound }, statuses.Order());
        var saved = await lists.Find(doc => doc.Id == list.Id).SingleAsync();
        Assert.Equal(new[] { remaining }, saved.LinkedUserIds);

        async Task<HttpStatusCode> RevokeAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, path);
            request.Headers.Add(HttpConstants.CallerIdHeader, list.OwnerId.ToString());
            await start.Task;
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }
    }
}
