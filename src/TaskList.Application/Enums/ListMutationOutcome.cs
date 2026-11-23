namespace TaskList.Application.Enums;

public enum ListMutationOutcome
{
    Succeeded,
    Forbidden,
    NotFound,
    OwnerConflict
}
