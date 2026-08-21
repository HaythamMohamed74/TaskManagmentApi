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
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var result = await taskService.CreateTaskAsync(currentUserService.UserId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await taskService.GetByIdAsync(currentUserService.UserId, id, ct);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TaskDto>>> GetAll(CancellationToken ct)
    {
        var result = await taskService.GetAllForUserAsync(currentUserService.UserId, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> UpdateStatus(Guid id, UpdateTaskStatusRequest request, CancellationToken ct)
    {
        var result = await taskService.UpdateStatusAsync(currentUserService.UserId, id, request, ct);
        return Ok(result);
    }
}
