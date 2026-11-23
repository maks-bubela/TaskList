using TaskList.Application.Enums;
using TaskList.Application.Models;

namespace TaskList.Application.Interfaces;

public interface ITaskListRepository
{
    Task CreateAsync(TaskListModel list, CancellationToken cancellationToken);

    Task<(TaskListModel? List, bool Exists)> GetAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken);

    Task<TaskListPage> ListAsync(
        Guid callerId, int page, int pageSize, CancellationToken cancellationToken);

    Task<ListMutationOutcome> RenameAsync(
        Guid listId, Guid callerId, string name, CancellationToken cancellationToken);

    Task<ListMutationOutcome> DeleteAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken);

    Task<AddLinkedUserOutcome> AddLinkedUserAsync(
        Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken);

    Task<(Guid[]? LinkedUserIds, bool Exists)> GetLinkedUsersAsync(
        Guid listId, Guid callerId, CancellationToken cancellationToken);

    Task<ListMutationOutcome> RemoveLinkedUserAsync(
        Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken);
}
