using System.Security.Claims;
using FastCart.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>
/// Base for versioned resource controllers. Establishes the §4.3 convention:
/// URL-segment versioning at <c>/api/v1</c>, RESTful nouns (e.g. /api/v1/products).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>The authenticated user's id from the JWT (sub / NameIdentifier), or null.</summary>
    protected string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    /// <summary>The authenticated user's id, or a 401 if unauthenticated.</summary>
    protected string CurrentUserIdRequired =>
        CurrentUserId ?? throw new UnauthorizedException();
}
