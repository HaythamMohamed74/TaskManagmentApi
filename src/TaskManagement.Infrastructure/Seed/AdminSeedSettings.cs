namespace TaskManagement.Infrastructure.Seed;

public class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    public string Name { get; set; } = "Administrator";
    public string Email { get; set; } = "admin@example.com";
    public string Password { get; set; } = "Admin@123";
}
