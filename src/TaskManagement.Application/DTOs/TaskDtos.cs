using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs;

public record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateTime CreatedAt,
    Guid UserId);

public record CreateTaskRequest(string Title, string? Description, TaskPriority Priority);

public record UpdateTaskStatusRequest(TaskItemStatus Status);
