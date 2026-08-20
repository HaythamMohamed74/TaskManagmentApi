using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Caching;

// Redis is required by the spec for caching Get-Task-by-Id, but a cache outage should
// degrade to "always hit the database" rather than take the whole endpoint down.
// Failures here are logged and swallowed so callers transparently fall back to the DB.
public class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private IDatabase Db => redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var value = await Db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis GET failed for key {Key}, falling back to database.", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await Db.StringSetAsync(key, json, ttl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis SET failed for key {Key}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await Db.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis DEL failed for key {Key}.", key);
        }
    }
}
