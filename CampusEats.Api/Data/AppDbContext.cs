using CampusEats.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItem>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Category).IsRequired().HasMaxLength(50);
            e.Property(x => x.Price).HasPrecision(10, 2);

            e.HasData(
                new MenuItem { Id = 1, Name = "Chicken Fried Rice", Description = "Wok-fried rice with chicken and egg", Price = 750m, Category = "Mains" },
                new MenuItem { Id = 2, Name = "Vegetable Kottu", Description = "Chopped roti with fresh vegetables", Price = 650m, Category = "Mains" },
                new MenuItem { Id = 3, Name = "Chicken Kottu", Description = "Chopped roti with chicken and spices", Price = 850m, Category = "Mains" },
                new MenuItem { Id = 4, Name = "Fish Bun", Description = "Soft bun with spicy fish filling", Price = 90m, Category = "Snacks" },
                new MenuItem { Id = 5, Name = "Egg Hopper", Description = "Crispy hopper with a soft egg", Price = 120m, Category = "Snacks" },
                new MenuItem { Id = 6, Name = "Iced Milk Tea", Description = "Cold and sweet milk tea", Price = 200m, Category = "Drinks" },
                new MenuItem { Id = 7, Name = "Watalappan", Description = "Coconut milk and jaggery pudding", Price = 300m, Category = "Desserts" }
            );
        });
    }
}
