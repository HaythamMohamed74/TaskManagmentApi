using TaskManagement.Application.DTOs;

namespace TaskManagement.Application.Interfaces;

public interface ITaskService
{
    Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto> GetByIdAsync(Guid userId, Guid taskId, CancellationToken ct = default);
    Task<List<TaskDto>> GetAllForUserAsync(Guid userId, CancellationToken ct = default);
    Task<TaskDto> UpdateStatusAsync(Guid userId, Guid taskId, UpdateTaskStatusRequest request, CancellationToken ct = default);
}
