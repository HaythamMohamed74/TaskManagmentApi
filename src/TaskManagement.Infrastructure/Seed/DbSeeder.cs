using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        AdminSeedSettings settings,
        ILogger logger)
    {
        await context.Database.MigrateAsync();

        var adminEmail = settings.Email.Trim().ToLowerInvariant();
        var adminExists = await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == adminEmail);
        if (adminExists)
            return;

        var admin = new User(settings.Name, settings.Email, passwordHasher.Hash(settings.Password), UserRole.Admin);
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded default admin user with email {Email}", settings.Email);
    }
}
