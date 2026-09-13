using Bitely.Models;
using Microsoft.EntityFrameworkCore;

namespace Bitely.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<FoodStall> FoodStalls { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
    }
}