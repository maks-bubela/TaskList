namespace TaskList.Api.Models;

public sealed record TaskListResponse(Guid Id, string Name, Guid OwnerId, DateTime CreatedAtUtc);
