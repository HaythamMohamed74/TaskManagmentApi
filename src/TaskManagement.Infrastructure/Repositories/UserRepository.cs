using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Interfaces;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower(), ct);

    public Task<List<User>> GetAllAsync(CancellationToken ct = default) =>
        context.Users.OrderBy(u => u.CreatedAt).ToListAsync(ct);

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        context.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        context.Users.AnyAsync(u => u.Email == email.Trim().ToLower(), ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
