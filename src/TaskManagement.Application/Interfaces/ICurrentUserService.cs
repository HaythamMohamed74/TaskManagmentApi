using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    UserRole Role { get; }
}
