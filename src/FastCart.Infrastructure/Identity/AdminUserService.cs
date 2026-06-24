using FastCart.Application.AdminUsers;
using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Domain.Common;
using FastCart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Identity;

/// <summary>Admin user &amp; role management (§6.14).</summary>
public sealed class AdminUserService : IAdminUserService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly AppDbContext _db;

    public AdminUserService(
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        AppDbContext db)
    {
        _users = users;
        _roles = roles;
        _db = db;
    }

    public async Task<PagedResult<AdminUserListItemDto>> ListAsync(string? userName, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var page = pageNumber < 1 ? 1 : pageNumber;
        var size = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(u => u.UserName != null && EF.Functions.ILike(u.UserName, $"%{userName}%"));
        }

        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.UserName)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        var items = new List<AdminUserListItemDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await _users.GetRolesAsync(u);
            items.Add(new AdminUserListItemDto(u.Id, u.UserName, u.Email, u.PhoneNumber, (IReadOnlyList<string>)roles, u.CreatedAt));
        }

        return new PagedResult<AdminUserListItemDto>(items, page, size, total);
    }

    public async Task<AdminUserDetailDto> GetAsync(string id, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(id)
            ?? throw new NotFoundException("User not found.");

        var roles = await _users.GetRolesAsync(user);
        var profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id, ct);

        return new AdminUserDetailDto(
            user.Id, user.UserName, user.Email, user.PhoneNumber,
            profile?.FirstName, profile?.LastName, profile?.ImageUrl,
            (IReadOnlyList<string>)roles, user.CreatedAt);
    }

    public async Task DeleteAsync(string id, string currentUserId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(id)
            ?? throw new NotFoundException("User not found.");

        if (id == currentUserId)
        {
            throw new BusinessRuleException("You cannot delete your own account.");
        }

        // Orders are kept (FK set-null); profile/addresses/cart/wishlist/reviews/tokens cascade.
        // CouponRedemption and ReturnRequest are RESTRICT — block with a clear message (§6.14).
        var hasRedemptions = await _db.CouponRedemptions.AnyAsync(r => r.UserId == id, ct);
        var hasReturns = await _db.ReturnRequests.AnyAsync(r => r.UserId == id, ct);
        if (hasRedemptions || hasReturns)
        {
            var blockers = new List<string>(2);
            if (hasReturns) blockers.Add("return requests");
            if (hasRedemptions) blockers.Add("coupon redemptions");
            throw new ConflictException(
                $"Cannot delete this user: they have {string.Join(" and ", blockers)} on record. Remove or reassign those first.");
        }

        var result = await _users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task AssignRoleAsync(string id, string roleId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(id)
            ?? throw new NotFoundException("User not found.");
        var role = await _roles.FindByIdAsync(roleId)
            ?? throw new NotFoundException("Role not found.");

        if (await _users.IsInRoleAsync(user, role.Name!))
        {
            return; // idempotent
        }

        var result = await _users.AddToRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task RemoveRoleAsync(string id, string roleId, string currentUserId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(id)
            ?? throw new NotFoundException("User not found.");
        var role = await _roles.FindByIdAsync(roleId)
            ?? throw new NotFoundException("Role not found.");

        if (id == currentUserId && string.Equals(role.Name, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("You cannot remove the Admin role from your own account.");
        }

        if (!await _users.IsInRoleAsync(user, role.Name!))
        {
            return; // idempotent
        }

        var result = await _users.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default) =>
        await _roles.Roles.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name!))
            .ToListAsync(ct);
}
