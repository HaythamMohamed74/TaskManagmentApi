using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Interfaces;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public class TaskRepository(AppDbContext context) : ITaskRepository
{
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        context.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<TaskItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        context.Tasks.Where(t => t.UserId == userId).ToListAsync(ct);

    public Task<bool> ExistsWithTitleOnDateAsync(Guid userId, string title, DateOnly date, CancellationToken ct = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var normalizedTitle = title.Trim();

        return context.Tasks.AnyAsync(t =>
            t.UserId == userId &&
            t.Title == normalizedTitle &&
            t.CreatedAt >= start && t.CreatedAt < end, ct);
    }

    public Task AddAsync(TaskItem task, CancellationToken ct = default)
    {
        context.Tasks.Add(task);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
