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

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _passwordHasher.Object,
            _jwtTokenGenerator.Object);

        _jwtTokenGenerator.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token-value");
    }

    [Fact]
    public async Task RegisterAsync_ThrowsConflict_WhenEmailAlreadyExists()
    {
        _userRepository.Setup(r => r.ExistsByEmailAsync("taken@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new RegisterRequest("Name", "taken@example.com", "Password1!");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_HashesPasswordAndReturnsTokens_ForNewUser()
    {
        _userRepository.Setup(r => r.ExistsByEmailAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash("Password1!")).Returns("hashed");
        _jwtTokenGenerator.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns(("jwt-token", DateTime.UtcNow.AddHours(1)));

        var request = new RegisterRequest("New User", "new@example.com", "Password1!");

        var result = await _sut.RegisterAsync(request, CancellationToken.None);

        Assert.Equal("jwt-token", result.Token);
        Assert.Equal("refresh-token-value", result.RefreshToken);
        Assert.Equal("new@example.com", result.User.Email);
        Assert.Equal(UserRole.User, result.User.Role);
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_WhenUserDoesNotExist()
    {
        _userRepository.Setup(r => r.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var request = new LoginRequest("missing@example.com", "whatever");

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_WhenPasswordDoesNotMatch()
    {
        var user = new User("Name", "user@example.com", "hashed", UserRole.User);
        _userRepository.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("hashed", "wrong-password")).Returns(false);

        var request = new LoginRequest("user@example.com", "wrong-password");

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokens_WhenCredentialsAreValid()
    {
        var user = new User("Name", "user@example.com", "hashed", UserRole.User);
        _userRepository.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("hashed", "correct-password")).Returns(true);
        _jwtTokenGenerator.Setup(j => j.GenerateToken(user)).Returns(("jwt-token", DateTime.UtcNow.AddHours(1)));

        var request = new LoginRequest("user@example.com", "correct-password");

        var result = await _sut.LoginAsync(request, CancellationToken.None);

        Assert.Equal("jwt-token", result.Token);
        Assert.Equal("refresh-token-value", result.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_ThrowsUnauthorized_WhenTokenDoesNotExist()
    {
        _refreshTokenRepository.Setup(r => r.GetByTokenAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.RefreshTokenAsync(new RefreshTokenRequest("missing"), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_ThrowsUnauthorized_WhenTokenIsExpired()
    {
        var user = new User("Name", "user@example.com", "hashed", UserRole.User);
        var expiredToken = new RefreshToken("expired-token", user.Id, TimeSpan.FromSeconds(-1));
        _refreshTokenRepository.Setup(r => r.GetByTokenAsync("expired-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.RefreshTokenAsync(new RefreshTokenRequest("expired-token"), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_ThrowsUnauthorized_WhenTokenAlreadyRevoked()
    {
        var user = new User("Name", "user@example.com", "hashed", UserRole.User);
        var revokedToken = new RefreshToken("revoked-token", user.Id, TimeSpan.FromDays(7));
        revokedToken.Revoke();
        _refreshTokenRepository.Setup(r => r.GetByTokenAsync("revoked-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedToken);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.RefreshTokenAsync(new RefreshTokenRequest("revoked-token"), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_RotatesToken_AndIssuesNewPair_WhenValid()
    {
        var user = new User("Name", "user@example.com", "hashed", UserRole.User);
        var validToken = new RefreshToken("valid-token", user.Id, TimeSpan.FromDays(7));

        _refreshTokenRepository.Setup(r => r.GetByTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(validToken);
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwtTokenGenerator.Setup(j => j.GenerateToken(user)).Returns(("new-jwt-token", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest("valid-token"), CancellationToken.None);

        Assert.False(validToken.IsActive);
        Assert.Equal("new-jwt-token", result.Token);
        Assert.Equal("refresh-token-value", result.RefreshToken);
        _refreshTokenRepository.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_RevokesActiveToken()
    {
        var user = new User("Name", "user@example.com", "hashed", UserRole.User);
        var validToken = new RefreshToken("valid-token", user.Id, TimeSpan.FromDays(7));
        _refreshTokenRepository.Setup(r => r.GetByTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(validToken);

        await _sut.RevokeRefreshTokenAsync(new RefreshTokenRequest("valid-token"), CancellationToken.None);

        Assert.False(validToken.IsActive);
        _refreshTokenRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
