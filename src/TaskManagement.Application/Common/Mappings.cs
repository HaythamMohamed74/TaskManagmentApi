using TaskManagement.Application.DTOs;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common;

public static class Mappings
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);

    public static TaskDto ToDto(this TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.Status, task.Priority, task.CreatedAt, task.UserId);
}
