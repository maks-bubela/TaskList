namespace TaskList.Application.Constants;

internal static class ExceptionMessageConstants
{
    public static readonly string InvalidPage = $"The page must be at least {PaginationConstants.FirstPage}.";

    public static readonly string InvalidPageSize =
        $"The page size must be between {PaginationConstants.MinimumPageSize} and {PaginationConstants.MaximumPageSize}.";

    public static readonly string InvalidNameLength =
        $"The name must contain {TaskListConstants.MinNameLength} to {TaskListConstants.MaxNameLength} characters after trimming.";
}
