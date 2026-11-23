using MongoDB.Driver;
using TaskList.Application.Constants;
using TaskList.Application.Enums;
using TaskList.Application.Interfaces;
using TaskList.Application.Models;

namespace TaskList.Infrastructure.MongoDB;

public sealed class MongoTaskListRepository(IMongoCollection<TaskListDocument> collection) : ITaskListRepository
{
    public Task CreateAsync(TaskListModel list, CancellationToken cancellationToken)
    {
        var document = new TaskListDocument
        {
            Id = list.Id,
            Name = list.Name,
            OwnerId = list.OwnerId,
            CreatedAtUtc = list.CreatedAtUtc
        };

        return collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<(TaskListModel? List, bool Exists)> GetAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken)
    {
        var listFilter = Builders<TaskListDocument>.Filter.Eq(document => document.Id, listId);
        var list = await collection.Find(listFilter & AccessFilter(callerId))
            .Project(document => new TaskListModel(
                document.Id, document.Name, document.OwnerId, document.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (list is not null)
        {
            return (list, true);
        }

        return (null, await ExistsAsync(listFilter, cancellationToken));
    }

    public async Task<TaskListPage> ListAsync(
        Guid callerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = AccessFilter(callerId);
        var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var offset = ((long)page - PaginationConstants.FirstPage) * pageSize;
        if (offset >= totalCount)
        {
            return new TaskListPage([], page, pageSize, totalCount);
        }

        var sort = Builders<TaskListDocument>.Sort
            .Descending(document => document.CreatedAtUtc)
            .Descending(document => document.Id);
        var projection = Builders<TaskListDocument>.Projection.Expression(
            document => new TaskListPageItem(document.Id, document.Name));

        var items = await collection.Aggregate()
            .Match(filter)
            .Sort(sort)
            .Skip(offset)
            .Limit(pageSize)
            .Project(projection)
            .ToListAsync(cancellationToken);

        return new TaskListPage(items, page, pageSize, totalCount);
    }

    public async Task<ListMutationOutcome> RenameAsync(
        Guid listId, Guid callerId, string name, CancellationToken cancellationToken)
    {
        var listFilter = Builders<TaskListDocument>.Filter.Eq(document => document.Id, listId);
        var update = Builders<TaskListDocument>.Update.Set(document => document.Name, name);
        var result = await collection.UpdateOneAsync(
            listFilter & AccessFilter(callerId), update, cancellationToken: cancellationToken);

        return result.MatchedCount == 1
            ? ListMutationOutcome.Succeeded
            : await ClassifyMissAsync(listFilter, cancellationToken);
    }

    public async Task<ListMutationOutcome> DeleteAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken)
    {
        var listFilter = Builders<TaskListDocument>.Filter.Eq(document => document.Id, listId);
        var result = await collection.DeleteOneAsync(listFilter & OwnerFilter(callerId), cancellationToken);

        return result.DeletedCount == 1
            ? ListMutationOutcome.Succeeded
            : await ClassifyMissAsync(listFilter, cancellationToken);
    }

    public async Task<AddLinkedUserOutcome> AddLinkedUserAsync(
        Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken)
    {
        var filter = Builders<TaskListDocument>.Filter;
        var listFilter = filter.Eq(document => document.Id, listId);
        var recipientIsNotOwner = filter.Ne(document => document.OwnerId, linkedUserId);
        var update = Builders<TaskListDocument>.Update.AddToSet(document => document.LinkedUserIds, linkedUserId);
        var result = await collection.UpdateOneAsync(
            listFilter & AccessFilter(callerId) & recipientIsNotOwner, update, cancellationToken: cancellationToken);

        if (result.MatchedCount == 1)
        {
            return result.ModifiedCount == 1 ? AddLinkedUserOutcome.Added : AddLinkedUserOutcome.AlreadyLinked;
        }

        var diagnostic = await ReadLinkMutationStateAsync(listFilter, callerId, linkedUserId, cancellationToken);
        if (diagnostic is not { } state)
        {
            return AddLinkedUserOutcome.NotFound;
        }

        return state.CallerHasAccess && state.TargetIsOwner
            ? AddLinkedUserOutcome.OwnerConflict
            : AddLinkedUserOutcome.Forbidden;
    }

    public async Task<(Guid[]? LinkedUserIds, bool Exists)> GetLinkedUsersAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken)
    {
        var listFilter = Builders<TaskListDocument>.Filter.Eq(document => document.Id, listId);
        var linkedUserIds = await collection.Find(listFilter & AccessFilter(callerId))
            .Project(document => document.LinkedUserIds)
            .FirstOrDefaultAsync(cancellationToken);

        return linkedUserIds is not null
            ? (linkedUserIds, true)
            : (null, await ExistsAsync(listFilter, cancellationToken));
    }

    public async Task<ListMutationOutcome> RemoveLinkedUserAsync(
        Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken)
    {
        var filter = Builders<TaskListDocument>.Filter;
        var listFilter = filter.Eq(document => document.Id, listId);
        var targetIsLinked = filter.AnyEq(document => document.LinkedUserIds, linkedUserId);
        var targetIsNotOwner = filter.Ne(document => document.OwnerId, linkedUserId);
        var update = Builders<TaskListDocument>.Update.Pull(document => document.LinkedUserIds, linkedUserId);
        var result = await collection.UpdateOneAsync(
            listFilter & AccessFilter(callerId) & targetIsLinked & targetIsNotOwner,
            update,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 1)
        {
            return ListMutationOutcome.Succeeded;
        }

        var diagnostic = await ReadLinkMutationStateAsync(listFilter, callerId, linkedUserId, cancellationToken);
        if (diagnostic is not { } state)
        {
            return ListMutationOutcome.NotFound;
        }

        if (!state.CallerHasAccess)
        {
            return ListMutationOutcome.Forbidden;
        }

        return state.TargetIsOwner ? ListMutationOutcome.OwnerConflict : ListMutationOutcome.NotFound;
    }

    private async Task<(bool CallerHasAccess, bool TargetIsOwner)?> ReadLinkMutationStateAsync(
        FilterDefinition<TaskListDocument> listFilter, Guid callerId, Guid linkedUserId,
        CancellationToken cancellationToken)
    {
        var diagnostic = await collection.Find(listFilter)
            .Project(document => new
            {
                CallerHasAccess = document.OwnerId == callerId || document.LinkedUserIds.Contains(callerId),
                TargetIsOwner = document.OwnerId == linkedUserId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return diagnostic is null ? null : (diagnostic.CallerHasAccess, diagnostic.TargetIsOwner);
    }

    private async Task<ListMutationOutcome> ClassifyMissAsync(
        FilterDefinition<TaskListDocument> listFilter, CancellationToken cancellationToken)
    {
        return await ExistsAsync(listFilter, cancellationToken)
            ? ListMutationOutcome.Forbidden
            : ListMutationOutcome.NotFound;
    }

    private Task<bool> ExistsAsync(
        FilterDefinition<TaskListDocument> listFilter, CancellationToken cancellationToken)
    {
        return collection.Find(listFilter)
            .Project(document => document.Id)
            .AnyAsync(cancellationToken);
    }

    private static FilterDefinition<TaskListDocument> AccessFilter(Guid callerId)
    {
        return OwnerFilter(callerId)
            | Builders<TaskListDocument>.Filter.AnyEq(document => document.LinkedUserIds, callerId);
    }

    private static FilterDefinition<TaskListDocument> OwnerFilter(Guid callerId)
    {
        return Builders<TaskListDocument>.Filter.Eq(document => document.OwnerId, callerId);
    }
}
