using Bitely.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bitely.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                if (context.Database.IsRelational())
                {
                    await context.Database.MigrateAsync();
                }

                string[] roles = { Roles.Consumer, Roles.FoodStallOwner };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                        logger.LogInformation("Seeded Identity role: {Role}", role);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during database initialization or role seeding.");
            }
        }
    }
}
