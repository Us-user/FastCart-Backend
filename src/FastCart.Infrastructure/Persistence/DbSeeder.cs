using FastCart.Domain.Common;
using FastCart.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastCart.Infrastructure.Persistence;

/// <summary>
/// Seeds the Customer/Admin roles and the initial admin from configuration
/// (Seed:AdminEmail / Seed:AdminPassword, §8/§9.2/§9.4). Idempotent — safe to run on
/// every startup.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(DbSeeder).FullName!);

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger?.LogWarning("Seed:AdminEmail/Password not configured; skipping admin seed.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
            {
                logger?.LogError("Failed to seed admin user: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
        }

        // The seeded account is the Boss — the top of the hierarchy and the only way
        // a Boss can exist, since the role is not grantable via the API (§4.4).
        if (!await userManager.IsInRoleAsync(admin, Roles.Boss))
        {
            await userManager.AddToRoleAsync(admin, Roles.Boss);
        }
    }
}
