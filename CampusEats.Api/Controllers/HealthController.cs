using CampusEats.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(
    AppDbContext db,
    IWebHostEnvironment env,
    ILogger<HealthController> logger) : ControllerBase
{
    /// <summary>
    /// Process liveness probe. Returns HTTP 200 if the ASP.NET Core process is running.
    /// Used by Railway and container orchestration.
    /// </summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        environment = env.EnvironmentName,
        timeUtc = DateTime.UtcNow
    });

    /// <summary>
    /// Database readiness probe. Executes a real PostgreSQL query ("SELECT 1").
    /// Returns HTTP 200 only when PostgreSQL is connected.
    /// Returns HTTP 503 when the database is unavailable.
    /// Does not expose credentials or connection strings.
    /// </summary>
    [HttpGet("db")]
    public async Task<IActionResult> GetDb()
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1;");
            return Ok(new
            {
                status = "healthy",
                database = "connected",
                provider = "PostgreSQL",
                timeUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database readiness probe failed: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                database = "disconnected",
                message = "Database is currently unavailable.",
                timeUtc = DateTime.UtcNow
            });
        }
    }
}
