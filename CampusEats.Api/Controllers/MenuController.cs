using CampusEats.Api.Data;
using CampusEats.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController(AppDbContext db, ILogger<MenuController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves all menu items directly from PostgreSQL.
    /// Exposes 'X-Data-Source: PostgreSQL' header.
    /// Returns HTTP 503 if the database is unavailable.
    /// </summary>
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

            Response.Headers.Append("X-Data-Source", "PostgreSQL");
            return Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database query failed while retrieving menu items: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Could not retrieve the menu. The database is currently unreachable.",
                error = "DatabaseUnavailable"
            });
        }
    }

    /// <summary>
    /// Retrieves a single menu item by ID directly from PostgreSQL.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MenuItem>> GetById(int id)
    {
        try
        {
            var item = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (item is null)
            {
                return NotFound(new { message = $"Menu item with ID {id} not found." });
            }

            Response.Headers.Append("X-Data-Source", "PostgreSQL");
            return Ok(item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database query failed while retrieving menu item ID {Id}: {Message}", id, ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Could not retrieve the menu item. The database is currently unreachable.",
                error = "DatabaseUnavailable"
            });
        }
    }
}
