using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.Models;

namespace olx_api.Services
{
    public static class DatabaseSeeder
    {
        public static async Task ApplyMigrationsAndSeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

            if (IsEnabled(Environment.GetEnvironmentVariable("AUTO_APPLY_MIGRATIONS") ?? configuration["Database:AutoApplyMigrations"]))
            {
                await context.Database.MigrateAsync();
            }

            await SeedAdminUserAsync(context, logger);
        }

        private static async Task SeedAdminUserAsync(ApplicationDbContext context, ILogger logger)
        {
            var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL")?.Trim().ToLowerInvariant();
            var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingAdmin is not null)
            {
                if (!string.Equals(existingAdmin.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    existingAdmin.Role = "Admin";
                    await context.SaveChangesAsync();
                }

                return;
            }

            var admin = new User
            {
                FullName = Environment.GetEnvironmentVariable("ADMIN_FULL_NAME")?.Trim() ?? "Platform Admin",
                Email = email,
                PhoneNumber = Environment.GetEnvironmentVariable("ADMIN_PHONE_NUMBER")?.Trim() ?? string.Empty,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Admin",
                IsVerified = true,
                AdQuotaRemaining = 0
            };

            context.Users.Add(admin);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded admin user {Email}", email);
        }

        private static bool IsEnabled(string? value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
