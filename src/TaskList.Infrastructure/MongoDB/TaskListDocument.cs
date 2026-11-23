namespace TaskList.Infrastructure.MongoDB;

public sealed class TaskListDocument
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid OwnerId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid[] LinkedUserIds { get; init; } = [];
}
