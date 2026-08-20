using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Interfaces;

namespace TaskManagement.Application.Services;

public class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new ConflictException($"A user with email '{request.Email}' already exists.");

        var user = new User(request.Name, request.Email, passwordHasher.Hash(request.Password), UserRole.User);

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);
        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
            throw new UnauthorizedException("Invalid email or password.");

        return await IssueTokensAsync(user, ct);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);
        return user.ToDto();
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var existingToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (existingToken is null || !existingToken.IsActive)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = await userRepository.GetByIdAsync(existingToken.UserId, ct)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        // Rotate: revoke the used token so it can't be replayed, issue a fresh pair.
        existingToken.Revoke();

        return await IssueTokensAsync(user, ct);
    }

    public async Task RevokeRefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var existingToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (existingToken is null || !existingToken.IsActive)
            return;

        existingToken.Revoke();
        await refreshTokenRepository.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (token, expiresAt) = jwtTokenGenerator.GenerateToken(user);
        var refreshTokenValue = jwtTokenGenerator.GenerateRefreshToken();

        var refreshToken = new RefreshToken(refreshTokenValue, user.Id, RefreshTokenLifetime);
        await refreshTokenRepository.AddAsync(refreshToken, ct);
        await refreshTokenRepository.SaveChangesAsync(ct);

        return new AuthResponse(token, expiresAt, refreshTokenValue, user.ToDto());
    }
}
