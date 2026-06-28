using System.Security.Claims;
using FastCart.Application.AdminUsers;
using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Admin user management (§6.14). Base route: <c>/api/v1/admin/users</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/users")]
[Authorize(Roles = Roles.Management)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _users;
    public AdminUsersController(IAdminUserService users) => _users = users;

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedException();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? userName,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<AdminUserListItemDto>>.Ok(await _users.ListAsync(userName, pageNumber, pageSize, ct)));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(ApiResponse<AdminUserDetailDto>.Ok(await _users.GetAsync(id, ct)));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _users.DeleteAsync(id, CurrentUserId, ct);
        return Ok(ApiResponse.Ok("User deleted."));
    }

    [HttpPost("{id}/roles")]
    public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        await _users.AssignRoleAsync(id, request.RoleId, CurrentUserId, ct);
        return Ok(ApiResponse.Ok("Role assigned."));
    }

    [HttpDelete("{id}/roles/{roleId}")]
    public async Task<IActionResult> RemoveRole(string id, string roleId, CancellationToken ct)
    {
        await _users.RemoveRoleAsync(id, roleId, CurrentUserId, ct);
        return Ok(ApiResponse.Ok("Role removed."));
    }
}

/// <summary>Admin role listing (§6.14). Base route: <c>/api/v1/admin/roles</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/roles")]
[Authorize(Roles = Roles.Management)]
public sealed class AdminRolesController : ControllerBase
{
    private readonly IAdminUserService _users;
    public AdminRolesController(IAdminUserService users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<RoleDto>>.Ok(await _users.ListRolesAsync(ct)));
}
