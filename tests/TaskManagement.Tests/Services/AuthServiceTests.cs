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
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_userRepository.Object, _passwordHasher.Object, _jwtTokenGenerator.Object);
    }

    [Fact]
    public async Task RegisterAsync_ThrowsConflict_WhenEmailAlreadyExists()
    {
        _userRepository.Setup(r => r.ExistsByEmailAsync("taken@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new RegisterRequest("Name", "taken@example.com", "Password1!");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_HashesPasswordAndReturnsToken_ForNewUser()
    {
        _userRepository.Setup(r => r.ExistsByEmailAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash("Password1!")).Returns("hashed");
        _jwtTokenGenerator.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns(("jwt-token", DateTime.UtcNow.AddHours(1)));

        var request = new RegisterRequest("New User", "new@example.com", "Password1!");

        var result = await _sut.RegisterAsync(request, CancellationToken.None);

        Assert.Equal("jwt-token", result.Token);
        Assert.Equal("new@example.com", result.User.Email);
        Assert.Equal(UserRole.User, result.User.Role);
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task LoginAsync_ReturnsToken_WhenCredentialsAreValid()
    {
        var user = new User("Name", "user@example.com", "hashed", UserRole.User);
        _userRepository.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("hashed", "correct-password")).Returns(true);
        _jwtTokenGenerator.Setup(j => j.GenerateToken(user)).Returns(("jwt-token", DateTime.UtcNow.AddHours(1)));

        var request = new LoginRequest("user@example.com", "correct-password");

        var result = await _sut.LoginAsync(request, CancellationToken.None);

        Assert.Equal("jwt-token", result.Token);
    }
}
