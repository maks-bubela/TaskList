namespace TaskList.Api.Constants;

internal static class TaskListRoutes
{
    public const string Collection = "api/task-lists";
    public const string List = "{listId}";
    public const string ListByIdName = "TaskLists.GetById";
    public const string LinkedUsers = List + "/linked-users";
    public const string LinkedUser = LinkedUsers + "/{userId}";
}
