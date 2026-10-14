using CampusEats.Api.Data;
using CampusEats.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MenuItem>>> GetAll()
    {
        var items = await db.MenuItems
            .AsNoTracking()
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MenuItem>> GetById(int id)
    {
        var item = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (item is null) return NotFound();
        return Ok(item);
    }
}
