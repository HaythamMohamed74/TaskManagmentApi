using Moq;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.Interfaces;
using TaskManagement.Application.Services;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Interfaces;
using Xunit;

namespace TaskManagement.Tests.Services;

public class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepository = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly Mock<ITaskProcessingQueue> _processingQueue = new();
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _sut = new TaskService(_taskRepository.Object, _cacheService.Object, _processingQueue.Object);
    }

    [Fact]
    public async Task CreateTaskAsync_ThrowsConflict_WhenDuplicateTitleSameDaySameUser()
    {
        var userId = Guid.NewGuid();
        _taskRepository.Setup(r => r.ExistsWithTitleOnDateAsync(userId, "Write report", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateTaskRequest("Write report", "desc", TaskPriority.Medium);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateTaskAsync(userId, request, CancellationToken.None));

        _taskRepository.Verify(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _processingQueue.Verify(q => q.QueueTask(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CreateTaskAsync_PersistsAndQueuesTask_WhenNoDuplicate()
    {
        var userId = Guid.NewGuid();
        _taskRepository.Setup(r => r.ExistsWithTitleOnDateAsync(userId, "Write report", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreateTaskRequest("Write report", "desc", TaskPriority.High);

        var result = await _sut.CreateTaskAsync(userId, request, CancellationToken.None);

        Assert.Equal("Write report", result.Title);
        Assert.Equal(TaskItemStatus.Pending, result.Status);
        _taskRepository.Verify(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _taskRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _processingQueue.Verify(q => q.QueueTask(result.Id), Times.Once);
    }

    [Fact]
    public async Task GetAllForUserAsync_SortsByPriorityDescendingThenCreatedAtAscending()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var low = new TaskItem("Low task", null, TaskPriority.Low, userId);
        var highOld = new TaskItem("High old", null, TaskPriority.High, userId);
        // CreatedAt has limited clock resolution; sleep briefly so the second High task
        // gets a strictly later timestamp and tie-breaking by CreatedAt is deterministic.
        await Task.Delay(20);
        var highNew = new TaskItem("High new", null, TaskPriority.High, userId);
        var medium = new TaskItem("Medium task", null, TaskPriority.Medium, userId);

        _taskRepository.Setup(r => r.GetAllByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([low, medium, highNew, highOld]);

        var result = await _sut.GetAllForUserAsync(userId, CancellationToken.None);

        Assert.Equal(4, result.Count);
        Assert.All(result.Take(2), t => Assert.Equal(TaskPriority.High, t.Priority));
        Assert.Equal(TaskPriority.Medium, result[2].Priority);
        Assert.Equal(TaskPriority.Low, result[3].Priority);
        // Among the two High tasks, the earlier-created one (highOld) should come first.
        Assert.Equal(highOld.Id, result[0].Id);
        Assert.Equal(highNew.Id, result[1].Id);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFound_WhenTaskBelongsToAnotherUser()
    {
        var owner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var task = new TaskItem("Someone else's task", null, TaskPriority.Low, owner);

        _cacheService.Setup(c => c.GetAsync<TaskDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskDto?)null);
        _taskRepository.Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(requester, task.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsFromCache_WithoutHittingRepository_WhenOwnedByRequester()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var cached = new TaskDto(taskId, "Cached", null, TaskItemStatus.Pending, TaskPriority.Low, DateTime.UtcNow, userId);

        _cacheService.Setup(c => c.GetAsync<TaskDto>($"task:{taskId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await _sut.GetByIdAsync(userId, taskId, CancellationToken.None);

        Assert.Equal(cached, result);
        _taskRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsNotFound_WhenNotOwner()
    {
        var owner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var task = new TaskItem("Task", null, TaskPriority.Low, owner);

        _taskRepository.Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateStatusAsync(requester, task.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatusAndRefreshesCache_WhenOwner()
    {
        var userId = Guid.NewGuid();
        var task = new TaskItem("Task", null, TaskPriority.Low, userId);

        _taskRepository.Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await _sut.UpdateStatusAsync(userId, task.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done), CancellationToken.None);

        Assert.Equal(TaskItemStatus.Done, result.Status);
        _taskRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheService.Verify(c => c.SetAsync($"task:{task.Id}", It.IsAny<TaskDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
