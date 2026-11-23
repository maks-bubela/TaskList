namespace TaskList.Application.Models;

public sealed record TaskListModel(Guid Id, string Name, Guid OwnerId, DateTime CreatedAtUtc);
