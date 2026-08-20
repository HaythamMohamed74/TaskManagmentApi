using TaskManagement.Domain.Entities;

namespace TaskManagement.Domain.Interfaces;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TaskItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ExistsWithTitleOnDateAsync(Guid userId, string title, DateOnly date, CancellationToken ct = default);
    Task AddAsync(TaskItem task, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
