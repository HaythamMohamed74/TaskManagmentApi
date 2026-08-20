namespace TaskManagement.Domain.Enums;

// Named TaskItemStatus (not TaskStatus) to avoid clashing with System.Threading.Tasks.TaskStatus.
public enum TaskItemStatus
{
    Pending = 0,
    InProgress = 1,
    Done = 2
}
