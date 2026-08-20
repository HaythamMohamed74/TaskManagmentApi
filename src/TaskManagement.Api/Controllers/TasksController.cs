using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController(ITaskService taskService, ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>Create a new task owned by the current user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var result = await taskService.CreateTaskAsync(currentUserService.UserId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Get a task by id. Only the owner may access it (cached in Redis).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await taskService.GetByIdAsync(currentUserService.UserId, id, ct);
        return Ok(result);
    }

    /// <summary>Get all tasks owned by the current user, sorted by priority then creation date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TaskDto>>> GetAll(CancellationToken ct)
    {
        var result = await taskService.GetAllForUserAsync(currentUserService.UserId, ct);
        return Ok(result);
    }

    /// <summary>Update the status of an owned task.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> UpdateStatus(Guid id, UpdateTaskStatusRequest request, CancellationToken ct)
    {
        var result = await taskService.UpdateStatusAsync(currentUserService.UserId, id, request, ct);
        return Ok(result);
    }
}
