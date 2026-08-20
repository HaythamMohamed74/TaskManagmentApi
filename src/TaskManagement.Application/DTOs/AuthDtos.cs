namespace TaskManagement.Application.DTOs;

public record RegisterRequest(string Name, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string RefreshToken, UserDto User);
