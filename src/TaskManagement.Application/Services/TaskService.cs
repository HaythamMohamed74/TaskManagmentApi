using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Interfaces;

namespace TaskManagement.Application.Services;

public class TaskService(
    ITaskRepository taskRepository,
    ICacheService cacheService,
    ITaskProcessingQueue processingQueue) : ITaskService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static string CacheKey(Guid taskId) => $"task:{taskId}";

    public async Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Business rule: no two tasks with the same title, for the same user, on the same day.
        if (await taskRepository.ExistsWithTitleOnDateAsync(userId, request.Title, today, ct))
            throw new ConflictException($"A task titled '{request.Title}' was already created today.");

        var task = new TaskItem(request.Title, request.Description, request.Priority, userId);

        await taskRepository.AddAsync(task, ct);
        await taskRepository.SaveChangesAsync(ct);

        // Hand off to the background worker to simulate further processing.
        processingQueue.QueueTask(task.Id);

        return task.ToDto();
    }

    public async Task<TaskDto> GetByIdAsync(Guid userId, Guid taskId, CancellationToken ct = default)
    {
        var cached = await cacheService.GetAsync<TaskDto>(CacheKey(taskId), ct);
        if (cached is not null)
        {
            EnsureOwnedBy(cached.UserId, userId);
            return cached;
        }

        var task = await taskRepository.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        EnsureOwnedBy(task.UserId, userId);

        var dto = task.ToDto();
        await cacheService.SetAsync(CacheKey(taskId), dto, CacheTtl, ct);
        return dto;
    }

    public async Task<List<TaskDto>> GetAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tasks = await taskRepository.GetAllByUserIdAsync(userId, ct);

        // Business rule: sort by priority (High first), then by creation date (oldest first).
        return tasks
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .Select(t => t.ToDto())
            .ToList();
    }

    public async Task<TaskDto> UpdateStatusAsync(Guid userId, Guid taskId, UpdateTaskStatusRequest request, CancellationToken ct = default)
    {
        var task = await taskRepository.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        EnsureOwnedBy(task.UserId, userId);

        task.UpdateStatus(request.Status);
        await taskRepository.SaveChangesAsync(ct);

        var dto = task.ToDto();
        // Refresh the cache so the next Get-by-id read isn't stale.
        await cacheService.SetAsync(CacheKey(taskId), dto, CacheTtl, ct);
        return dto;
    }

    private static void EnsureOwnedBy(Guid taskOwnerId, Guid requestingUserId)
    {
        // Treated as not-found (rather than forbidden) so a user can't probe for other users' task ids.
        if (taskOwnerId != requestingUserId)
            throw new NotFoundException(nameof(TaskItem), taskOwnerId);
    }
}
