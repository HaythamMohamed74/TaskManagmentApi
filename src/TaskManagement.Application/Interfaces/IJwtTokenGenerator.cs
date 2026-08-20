using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
    string GenerateRefreshToken();
}
