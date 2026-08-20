using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Interfaces;
using TaskManagement.Infrastructure.Auth;
using TaskManagement.Infrastructure.BackgroundProcessing;
using TaskManagement.Infrastructure.Caching;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Repositories;
using TaskManagement.Infrastructure.Seed;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis")!);
            // Don't crash app startup if Redis isn't reachable yet; commands will simply
            // fail (and surface as 500s on the caching path) until it comes up.
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        services.AddSingleton<InMemoryTaskProcessingQueue>();
        services.AddSingleton<ITaskProcessingQueue>(sp => sp.GetRequiredService<InMemoryTaskProcessingQueue>());
        services.AddHostedService<TaskProcessingBackgroundService>();

        services.Configure<AdminSeedSettings>(configuration.GetSection(AdminSeedSettings.SectionName));
        services.AddScoped(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminSeedSettings>>().Value);

        return services;
    }
}
