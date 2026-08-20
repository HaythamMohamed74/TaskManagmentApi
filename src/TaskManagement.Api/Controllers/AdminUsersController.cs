using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminUsersController(IUserAdminService userAdminService) : ControllerBase
{
    /// <summary>List all users. Admin only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserDto>>> GetAll(CancellationToken ct)
    {
        var result = await userAdminService.GetAllUsersAsync(ct);
        return Ok(result);
    }

    /// <summary>Create a new user. Admin only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await userAdminService.CreateUserAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), null, result);
    }

    /// <summary>Delete (soft-delete) a user. Admin only.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await userAdminService.DeleteUserAsync(id, ct);
        return NoContent();
    }
}
