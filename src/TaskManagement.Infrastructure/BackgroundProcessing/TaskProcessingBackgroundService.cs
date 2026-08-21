using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Interfaces;

namespace TaskManagement.Infrastructure.BackgroundProcessing;

public class TaskProcessingBackgroundService(
    InMemoryTaskProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<TaskProcessingBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan SimulatedProcessingDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var taskId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(taskId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to background-process task {TaskId}", taskId);
            }
        }
    }

    private async Task ProcessAsync(Guid taskId, CancellationToken ct)
    {
        await Task.Delay(SimulatedProcessingDelay, ct);

        using var scope = scopeFactory.CreateScope();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var task = await taskRepository.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            logger.LogWarning("Queued task {TaskId} no longer exists, skipping.", taskId);
            return;
        }

        task.UpdateStatus(TaskItemStatus.InProgress);
        await taskRepository.SaveChangesAsync(ct);

        await cacheService.SetAsync($"task:{taskId}", task.ToDto(), CacheTtl, ct);

        logger.LogInformation("Task {TaskId} processed by background worker, status -> InProgress", taskId);
    }
}
