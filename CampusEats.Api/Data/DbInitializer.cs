using CampusEats.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Data;

public static class DbInitializer
{
    private static readonly MenuItem[] SeedItems =
    [
        new() { Id = 1, Name = "Chicken Fried Rice", Description = "Wok-fried rice with chicken and egg", Price = 750m, Category = "Mains", IsAvailable = true },
        new() { Id = 2, Name = "Vegetable Kottu", Description = "Chopped roti with fresh vegetables", Price = 650m, Category = "Mains", IsAvailable = true },
        new() { Id = 3, Name = "Chicken Kottu", Description = "Chopped roti with chicken and spices", Price = 850m, Category = "Mains", IsAvailable = true },
        new() { Id = 4, Name = "Fish Bun", Description = "Soft bun with spicy fish filling", Price = 90m, Category = "Snacks", IsAvailable = true },
        new() { Id = 5, Name = "Egg Hopper", Description = "Crispy hopper with a soft egg", Price = 120m, Category = "Snacks", IsAvailable = true },
        new() { Id = 6, Name = "Iced Milk Tea", Description = "Cold and sweet milk tea", Price = 200m, Category = "Drinks", IsAvailable = true },
        new() { Id = 7, Name = "Watalappan", Description = "Coconut milk and jaggery pudding", Price = 300m, Category = "Desserts", IsAvailable = true }
    ];

    public static async Task ApplyMigrationsAndSeedAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            logger.LogInformation("Applying EF Core database migrations...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");

            // Idempotent check: ensure all 7 seed items exist
            var existingCount = await db.MenuItems.CountAsync();
            if (existingCount < SeedItems.Length)
            {
                logger.LogInformation("Verifying and seeding required menu items...");
                foreach (var item in SeedItems)
                {
                    var exists = await db.MenuItems.AnyAsync(m => m.Id == item.Id);
                    if (!exists)
                    {
                        db.MenuItems.Add(new MenuItem
                        {
                            Id = item.Id,
                            Name = item.Name,
                            Description = item.Description,
                            Price = item.Price,
                            Category = item.Category,
                            IsAvailable = item.IsAvailable
                        });
                    }
                }
                await db.SaveChangesAsync();
                logger.LogInformation("Seeding completed successfully.");
            }
            else
            {
                logger.LogInformation("Menu items table verified. {Count} records present.", existingCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration/seeding encountered an error: {Message}", ex.Message);
            // Do not rethrow so application process can still start and serve /api/health,
            // while /api/health/db will correctly report HTTP 503 unhealthy.
        }
    }
}
