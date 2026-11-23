using System.ComponentModel.DataAnnotations;
using TaskList.Application.Enums;
using TaskList.Application.Interfaces;
using TaskList.Application.Models;
using TaskList.Application.Services;
using TaskList.UnitTests.Constants;
using Xunit;

namespace TaskList.UnitTests;

public sealed class TaskListServiceTests
{
    [Fact]
    public async Task CreateAcceptsNameAtMaximumLengthAfterTrimmingAndPreservesInternalSpaces()
    {
        const string namePrefix = "Shopping  ";
        var expected = namePrefix + new string('x', ValidationTestConstants.MaxNameLength - namePrefix.Length);
        var repository = new RecordingRepository();
        var service = new TaskListService(repository);
        var callerId = Guid.NewGuid();

        var created = await service.CreateAsync(callerId, $" \t{expected}\n ", CancellationToken.None);

        Assert.Equal(expected, created.Name);
        Assert.Equal(callerId, created.OwnerId);
        Assert.Same(created, repository.Saved);
    }

    [Fact]
    public async Task CreateRejectsNameLongerThan255AfterTrimmingWithoutWriting()
    {
        var repository = new RecordingRepository();
        var service = new TaskListService(repository);
        var name = $" {new string('x', ValidationTestConstants.TooLongNameLength)} ";

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(Guid.NewGuid(), name, CancellationToken.None));

        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task AddLinkedUserRejectsEmptyTargetBeforeWriting()
    {
        var service = new TaskListService(new RecordingRepository());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddLinkedUserAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, CancellationToken.None));

        Assert.Equal("linkedUserId cannot be empty.", exception.Message);
    }

    private sealed class RecordingRepository : ITaskListRepository
    {
        public TaskListModel? Saved { get; private set; }

        public Task CreateAsync(TaskListModel list, CancellationToken cancellationToken)
        {
            Saved = list;
            return Task.CompletedTask;
        }

        public Task<(TaskListModel? List, bool Exists)> GetAsync(
            Guid listId, Guid callerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskListPage> ListAsync(
            Guid callerId, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ListMutationOutcome> RenameAsync(
            Guid listId, Guid callerId, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ListMutationOutcome> DeleteAsync(
            Guid listId, Guid callerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AddLinkedUserOutcome> AddLinkedUserAsync(
            Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(Guid[]? LinkedUserIds, bool Exists)> GetLinkedUsersAsync(
            Guid listId, Guid callerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ListMutationOutcome> RemoveLinkedUserAsync(
            Guid listId, Guid callerId, Guid linkedUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
