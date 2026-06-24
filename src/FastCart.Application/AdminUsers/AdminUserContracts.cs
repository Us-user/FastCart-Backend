using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;

namespace FastCart.Application.AdminUsers;

// ── Admin users & roles (§6.14) ──────────────────────────────────────────────

public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItemDto>> ListAsync(string? userName, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<AdminUserDetailDto> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Delete a user. Blocks (409) when RESTRICT dependents exist (coupon redemptions / returns).</summary>
    Task DeleteAsync(string id, string currentUserId, CancellationToken ct = default);

    Task AssignRoleAsync(string id, string roleId, CancellationToken ct = default);
    Task RemoveRoleAsync(string id, string roleId, string currentUserId, CancellationToken ct = default);

    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default);
}

public sealed record AdminUserListItemDto(
    string Id, string? UserName, string? Email, string? PhoneNumber, IReadOnlyList<string> Roles, DateTime CreatedAt);

public sealed record AdminUserDetailDto(
    string Id, string? UserName, string? Email, string? PhoneNumber,
    string? FirstName, string? LastName, string? ImageUrl,
    IReadOnlyList<string> Roles, DateTime CreatedAt);

public sealed record RoleDto(string Id, string Name);

public sealed record AssignRoleRequest
{
    [Required]
    public string RoleId { get; init; } = default!;
}
