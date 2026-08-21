using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Interfaces;

namespace TaskManagement.Application.Services;

public class UserAdminService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher) : IUserAdminService
{
    public async Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = await userRepository.GetAllAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new ConflictException($"A user with email '{request.Email}' already exists.");

        var user = new User(request.Name, request.Email, passwordHasher.Hash(request.Password), request.Role);

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);

        return user.ToDto();
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

        user.MarkDeleted();
        await userRepository.SaveChangesAsync(ct);
    }
}
