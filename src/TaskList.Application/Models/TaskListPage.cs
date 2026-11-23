namespace TaskList.Application.Models;

public sealed record TaskListPage(
    IReadOnlyList<TaskListPageItem> Items, int Page, int PageSize, long TotalCount);

public sealed record TaskListPageItem(Guid Id, string Name);
