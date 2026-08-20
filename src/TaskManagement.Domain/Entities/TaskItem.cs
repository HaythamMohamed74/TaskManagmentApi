using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class TaskItem
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid UserId { get; private set; }

    private TaskItem() { }

    public TaskItem(string title, string? description, TaskPriority priority, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Id = Guid.NewGuid();
        Title = title.Trim();
        Description = description?.Trim();
        Priority = priority;
        Status = TaskItemStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UserId = userId;
    }

    public void UpdateStatus(TaskItemStatus status) => Status = status;
}
