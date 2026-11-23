namespace TaskList.Infrastructure.Constants;

internal static class TaskListMongoConstants
{
    public const string CollectionName = "taskLists";

    public const string NameField = "name";
    public const string OwnerIdField = "ownerId";
    public const string CreatedAtUtcField = "createdAtUtc";
    public const string LinkedUserIdsField = "linkedUserIds";

    public const string OwnerIndexName = "ownerId_createdAtUtc_desc_id_desc";
    public const string LinkedUsersIndexName = "linkedUserIds_createdAtUtc_desc_id_desc";
}
