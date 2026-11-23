using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using TaskList.Infrastructure.MongoDB;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class MongoMappingTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task StoresListOwnerAndLinkedUserIdsAsStandardUuids()
    {
        await using var factory = new TaskListApiFactory(mongo.ConnectionString);
        var collection = factory.Services.GetRequiredService<IMongoCollection<TaskListDocument>>();

        var document = new TaskListDocument
        {
            Id = Guid.NewGuid(),
            Name = "Shopping",
            OwnerId = Guid.NewGuid(),
            CreatedAtUtc = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc),
            LinkedUserIds = [Guid.NewGuid(), Guid.NewGuid()]
        };

        await collection.InsertOneAsync(document);

        var raw = await collection.Database.GetCollection<BsonDocument>(collection.CollectionNamespace.CollectionName)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .SingleAsync();

        AssertStandardUuid(raw[MongoTestConstants.IdField], document.Id);
        AssertStandardUuid(raw[MongoTestConstants.OwnerIdField], document.OwnerId);
        Assert.Collection(raw[MongoTestConstants.LinkedUserIdsField].AsBsonArray,
            first => AssertStandardUuid(first, document.LinkedUserIds[0]),
            second => AssertStandardUuid(second, document.LinkedUserIds[1]));
    }

    private static void AssertStandardUuid(BsonValue value, Guid expected)
    {
        var binary = value.AsBsonBinaryData;
        Assert.Equal(BsonBinarySubType.UuidStandard, binary.SubType);
        Assert.Equal(expected, binary.ToGuid());
    }
}
