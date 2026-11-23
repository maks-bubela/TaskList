using System.ComponentModel.DataAnnotations;
using TaskList.Application.Constants;
using TaskList.Application.Enums;
using TaskList.Application.Interfaces;
using TaskList.Application.Models;

namespace TaskList.Application.Services;

public sealed class TaskListService(ITaskListRepository repository)
{
    public Task<(TaskListModel? List, bool Exists)> GetAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken)
    {
        ValidateGuid(listId, nameof(listId));
        ValidateGuid(callerId, nameof(callerId));
        return repository.GetAsync(listId, callerId, cancellationToken);
    }

    public Task<TaskListPage> ListAsync(
        Guid callerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidateGuid(callerId, nameof(callerId));
        if (page < PaginationConstants.FirstPage)
        {
            throw new ValidationException(ExceptionMessageConstants.InvalidPage);
        }

        if (pageSize is < PaginationConstants.MinimumPageSize or > PaginationConstants.MaximumPageSize)
        {
            throw new ValidationException(ExceptionMessageConstants.InvalidPageSize);
        }

        return repository.ListAsync(callerId, page, pageSize, cancellationToken);
    }

    public async Task<TaskListModel> CreateAsync(
        Guid callerId, string? name, CancellationToken cancellationToken)
    {
        ValidateGuid(callerId, nameof(callerId));
        var normalizedName = NormalizeName(name);

        // MongoDB stores BSON dates as Unix milliseconds.
        var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createdAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;
        var list = new TaskListModel(Guid.NewGuid(), normalizedName, callerId, createdAtUtc);

        await repository.CreateAsync(list, cancellationToken);
        return list;
    }

    public Task<ListMutationOutcome> RenameAsync(
        Guid listId, Guid callerId, string? name, CancellationToken cancellationToken)
    {
        ValidateGuid(listId, nameof(listId));
        ValidateGuid(callerId, nameof(callerId));
        return repository.RenameAsync(listId, callerId, NormalizeName(name), cancellationToken);
    }

    public Task<ListMutationOutcome> DeleteAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken)
    {
        ValidateGuid(listId, nameof(listId));
        ValidateGuid(callerId, nameof(callerId));
        return repository.DeleteAsync(listId, callerId, cancellationToken);
    }

    public Task<AddLinkedUserOutcome> AddLinkedUserAsync(
        Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken)
    {
        ValidateGuid(listId, nameof(listId));
        ValidateGuid(callerId, nameof(callerId));
        ValidateGuid(linkedUserId, nameof(linkedUserId));

        return repository.AddLinkedUserAsync(listId, callerId, linkedUserId, cancellationToken);
    }

    public Task<(Guid[]? LinkedUserIds, bool Exists)> GetLinkedUsersAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken)
    {
        ValidateGuid(listId, nameof(listId));
        ValidateGuid(callerId, nameof(callerId));
        return repository.GetLinkedUsersAsync(listId, callerId, cancellationToken);
    }

    public Task<ListMutationOutcome> RemoveLinkedUserAsync(
        Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken)
    {
        ValidateGuid(listId, nameof(listId));
        ValidateGuid(callerId, nameof(callerId));
        ValidateGuid(linkedUserId, nameof(linkedUserId));
        return repository.RemoveLinkedUserAsync(listId, callerId, linkedUserId, cancellationToken);
    }

    private static void ValidateGuid(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException($"{parameterName} cannot be empty.");
        }
    }

    private static string NormalizeName(string? name)
    {
        var normalizedName = name?.Trim();
        if (normalizedName is null ||
            normalizedName.Length is < TaskListConstants.MinNameLength or > TaskListConstants.MaxNameLength)
        {
            throw new ValidationException(ExceptionMessageConstants.InvalidNameLength);
        }

        return normalizedName;
    }
}
