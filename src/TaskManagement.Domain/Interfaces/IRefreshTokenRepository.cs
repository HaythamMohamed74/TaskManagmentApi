using TaskManagement.Domain.Entities;

namespace TaskManagement.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
