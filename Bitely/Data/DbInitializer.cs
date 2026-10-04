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
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                if (context.Database.IsRelational())
                {
                    await context.Database.MigrateAsync();
                }

                // 1. Seed Roles
                string[] roles = { Roles.Consumer, Roles.FoodStallOwner };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                        logger.LogInformation("Seeded Identity role: {Role}", role);
                    }
                }

                // 2. Seed Default Food Stall Owner User
                var ownerEmail = "shop@mail.com";
                var defaultOwner = await userManager.FindByEmailAsync(ownerEmail);
                if (defaultOwner == null)
                {
                    defaultOwner = new ApplicationUser
                    {
                        UserName = ownerEmail,
                        Email = ownerEmail,
                        Name = "Shopkeeper",
                        EmailConfirmed = true
                    };
                    var result = await userManager.CreateAsync(defaultOwner, "shop123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(defaultOwner, Roles.FoodStallOwner);
                        logger.LogInformation("Seeded default food stall owner: {Email}", ownerEmail);
                    }
                }

                // 3. Seed Default Consumer User
                var consumerEmail = "cust@mail.com";
                var defaultConsumer = await userManager.FindByEmailAsync(consumerEmail);
                if (defaultConsumer == null)
                {
                    defaultConsumer = new ApplicationUser
                    {
                        UserName = consumerEmail,
                        Email = consumerEmail,
                        Name = "Customer",
                        EmailConfirmed = true
                    };
                    var result = await userManager.CreateAsync(defaultConsumer, "cust123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(defaultConsumer, Roles.Consumer);
                        logger.LogInformation("Seeded default consumer: {Email}", consumerEmail);
                    }
                }

                // 4. Seed Food Stalls and Menu Items if none exist
                if (!await context.FoodStalls.AnyAsync())
                {
                    var ownerId = defaultOwner!.Id;

                    var stalls = new List<FoodStall>
                    {
                        new FoodStall
                        {
                            Name = "Darbar Mug Pulav",
                            Description = "Special Gujarati spicy pulav, bhat, and savory street meals.",
                            Location = "Food Court - Stall 1",
                            IsOpen = true,
                            OwnerId = ownerId,
                            MenuItems = new List<MenuItem>
                            {
                                new MenuItem
                                {
                                    Name = "Hyderabadi Pulav",
                                    Category = "Rice",
                                    Price = 60.00m,
                                    Description = "Special spiced green rice served with curd chutney",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Mug Pulav",
                                    Category = "Rice",
                                    Price = 50.00m,
                                    Description = "Spicy red chili flavored masala rice topped with crispy sev",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Special Panner Pulav",
                                    Category = "Rice",
                                    Price = 120.00m,
                                    Description = "Signature boiled Panner pulav prepared with fresh butter & onions",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Masala Chaas",
                                    Category = "Beverages",
                                    Price = 15.00m,
                                    Description = "Fresh chilled spiced buttermilk with roasted cumin",
                                    IsAvailable = true
                                }
                            }
                        },
                        new FoodStall
                        {
                            Name = "Pizza & Burger Corner",
                            Description = "Delicious cheesy pizzas, grilled burgers, crispy sides and cold drinks.",
                            Location = "Food Court - Stall 2",
                            IsOpen = true,
                            OwnerId = ownerId,
                            MenuItems = new List<MenuItem>
                            {
                                new MenuItem
                                {
                                    Name = "Classic Margherita Pizza",
                                    Category = "Pizza",
                                    Price = 149.00m,
                                    Description = "Rich tomato sauce, melted mozzarella cheese and basil",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Paneer Tikka Burger",
                                    Category = "Burgers",
                                    Price = 99.00m,
                                    Description = "Spiced grilled paneer patty with mint mayo and fresh lettuce",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Peri Peri French Fries",
                                    Category = "Snacks",
                                    Price = 69.00m,
                                    Description = "Crispy golden french fries tossed in spicy peri peri seasoning",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Cold Coffee",
                                    Category = "Beverages",
                                    Price = 59.00m,
                                    Description = "Rich blended cold coffee with chocolate drizzle",
                                    IsAvailable = true
                                }
                            }
                        },
                        new FoodStall
                        {
                            Name = "South Indian Express",
                            Description = "Authentic crispy dosas, steaming hot idlis and aromatic filter coffee.",
                            Location = "Food Court - Stall 3",
                            IsOpen = true,
                            OwnerId = ownerId,
                            MenuItems = new List<MenuItem>
                            {
                                new MenuItem
                                {
                                    Name = "Mysore Masala Dosa",
                                    Category = "Dosas",
                                    Price = 85.00m,
                                    Description = "Crispy dosa layered with spicy red chutney and potato filling",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Steamed Idli Sambar (2 Pcs)",
                                    Category = "South Indian",
                                    Price = 50.00m,
                                    Description = "Fluffy steamed rice cakes served with hot sambar and coconut chutney",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Crispy Medu Vada (2 Pcs)",
                                    Category = "South Indian",
                                    Price = 60.00m,
                                    Description = "Golden fried lentil donuts with crispy crust",
                                    IsAvailable = true
                                },
                                new MenuItem
                                {
                                    Name = "Filter Coffee",
                                    Category = "Beverages",
                                    Price = 30.00m,
                                    Description = "Traditional hot brewed South Indian filter coffee",
                                    IsAvailable = true
                                }
                            }
                        }
                    };

                    await context.FoodStalls.AddRangeAsync(stalls);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded demo food stalls and menu items.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during database initialization or data seeding.");
            }
        }
    }
}
