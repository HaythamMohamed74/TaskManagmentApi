namespace TaskManagement.Domain.Enums;

// avoids clashing with System.Threading.Tasks.TaskStatus
public enum TaskItemStatus
{
    Pending = 0,
    InProgress = 1,
    Done = 2
}
