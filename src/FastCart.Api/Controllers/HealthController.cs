using FastCart.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>
/// Liveness/readiness endpoint (§6.16, §9.6) for Render health checks and uptime pings.
/// Public, unversioned, served at the root <c>/health</c>.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() =>
        Ok(ApiResponse<object>.Ok(
            new { status = "Healthy", utc = DateTime.UtcNow },
            "FastCart API is running."));
}
