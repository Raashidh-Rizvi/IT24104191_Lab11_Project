using Microsoft.AspNetCore.Mvc;

namespace CampusEats.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        environment = env.EnvironmentName,
        timeUtc = DateTime.UtcNow
    });
}
