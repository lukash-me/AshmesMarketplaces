using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            environment = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().EnvironmentName,
            checkedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
