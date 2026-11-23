namespace TaskList.Api.Constants;

internal static class ExceptionMessageConstants
{
    public const string MissingMongoConnectionString =
        MongoConfigurationConstants.Section + ":" + MongoConfigurationConstants.ConnectionString + " is required.";

    public const string MissingMongoDatabaseName =
        MongoConfigurationConstants.Section + ":" + MongoConfigurationConstants.DatabaseName + " is required.";

    public const string UnexpectedError = "An unexpected error occurred.";
    public const string MissingTaskListLocation = "Could not generate the task list location.";
}
