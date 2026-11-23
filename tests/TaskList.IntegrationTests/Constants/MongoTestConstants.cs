namespace TaskList.IntegrationTests.Constants;

internal static class MongoTestConstants
{
    public const string Image = "mongo:8.0";
    public const string DatabasePrefix = "tasklist_tests_";
    public const string ConnectionStringKey = "MongoDB:ConnectionString";
    public const string DatabaseNameKey = "MongoDB:DatabaseName";
    public const string IdField = "_id";
    public const string OwnerIdField = "ownerId";
    public const string LinkedUserIdsField = "linkedUserIds";
}
