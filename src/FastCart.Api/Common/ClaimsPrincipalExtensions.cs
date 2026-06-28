using System.Security.Claims;
using FastCart.Domain.Common;

namespace FastCart.Api.Common;

/// <summary>Helpers for reading identity/role from the JWT principal (§4.4).</summary>
public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

    /// <summary>True for any management role (Admin, SuperAdmin or Boss).</summary>
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.SuperAdmin) || user.IsInRole(Roles.Boss);
}
