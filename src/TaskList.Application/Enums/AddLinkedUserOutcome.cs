namespace TaskList.Application.Enums;

public enum AddLinkedUserOutcome
{
    Added,
    AlreadyLinked,
    Forbidden,
    NotFound,
    OwnerConflict
}
