using CampusEats.Api.Data;
using CampusEats.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController(AppDbContext db, ILogger<MenuController> logger) : ControllerBase
{
    private static readonly List<MenuItem> FallbackItems =
    [
        new() { Id = 1, Name = "Chicken Fried Rice", Description = "Wok-fried rice with chicken and egg", Price = 750m, Category = "Mains" },
        new() { Id = 2, Name = "Vegetable Kottu", Description = "Chopped roti with fresh vegetables", Price = 650m, Category = "Mains" },
        new() { Id = 3, Name = "Chicken Kottu", Description = "Chopped roti with chicken and spices", Price = 850m, Category = "Mains" },
        new() { Id = 4, Name = "Fish Bun", Description = "Soft bun with spicy fish filling", Price = 90m, Category = "Snacks" },
        new() { Id = 5, Name = "Egg Hopper", Description = "Crispy hopper with a soft egg", Price = 120m, Category = "Snacks" },
        new() { Id = 6, Name = "Iced Milk Tea", Description = "Cold and sweet milk tea", Price = 200m, Category = "Drinks" },
        new() { Id = 7, Name = "Watalappan", Description = "Coconut milk and jaggery pudding", Price = 300m, Category = "Desserts" }
    ];

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MenuItem>>> GetAll()
    {
        try
        {
            var items = await db.MenuItems
                .AsNoTracking()
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Name)
                .ToListAsync();

            if (items.Count > 0)
            {
                return Ok(items);
            }

            return Ok(FallbackItems);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database query failed. Returning seeded fallback menu items: {Message}", ex.Message);
            Response.Headers.Append("X-Data-Source", "Fallback");
            return Ok(FallbackItems);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MenuItem>> GetById(int id)
    {
        try
        {
            var item = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (item is not null) return Ok(item);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database query failed for item id {Id}", id);
        }

        var fallback = FallbackItems.FirstOrDefault(m => m.Id == id);
        return fallback is not null ? Ok(fallback) : NotFound();
    }
}
