using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TaskList.Infrastructure.Constants;

namespace TaskList.Infrastructure.MongoDB;

public static class MongoTaskLists
{
    static MongoTaskLists()
    {
        var guidSerializer = new GuidSerializer(GuidRepresentation.Standard);

        BsonClassMap.RegisterClassMap<TaskListDocument>(map =>
        {
            map.MapIdMember(document => document.Id).SetSerializer(guidSerializer);
            map.MapMember(document => document.Name).SetElementName(TaskListMongoConstants.NameField);
            map.MapMember(document => document.OwnerId).SetElementName(TaskListMongoConstants.OwnerIdField).SetSerializer(guidSerializer);
            map.MapMember(document => document.CreatedAtUtc).SetElementName(TaskListMongoConstants.CreatedAtUtcField);
            map.MapMember(document => document.LinkedUserIds)
                .SetElementName(TaskListMongoConstants.LinkedUserIdsField)
                .SetSerializer(new ArraySerializer<Guid>(guidSerializer));
        });
    }

    public static IMongoCollection<TaskListDocument> GetCollection(IMongoClient client, string databaseName)
    {
        return client.GetDatabase(databaseName).GetCollection<TaskListDocument>(TaskListMongoConstants.CollectionName);
    }

    public static Task EnsureIndexesAsync(
        IMongoCollection<TaskListDocument> collection, CancellationToken cancellationToken)
    {
        var keys = Builders<TaskListDocument>.IndexKeys;
        return collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<TaskListDocument>(
                keys.Ascending(list => list.OwnerId)
                    .Descending(list => list.CreatedAtUtc)
                    .Descending(list => list.Id),
                new CreateIndexOptions { Name = TaskListMongoConstants.OwnerIndexName }),
            new CreateIndexModel<TaskListDocument>(
                keys.Ascending(list => list.LinkedUserIds)
                    .Descending(list => list.CreatedAtUtc)
                    .Descending(list => list.Id),
                new CreateIndexOptions { Name = TaskListMongoConstants.LinkedUsersIndexName })
        ], cancellationToken);
    }
}
