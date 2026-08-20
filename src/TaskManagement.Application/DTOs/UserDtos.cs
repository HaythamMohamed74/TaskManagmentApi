using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs;

public record UserDto(Guid Id, string Name, string Email, UserRole Role, DateTime CreatedAt);

public record CreateUserRequest(string Name, string Email, string Password, UserRole Role);
