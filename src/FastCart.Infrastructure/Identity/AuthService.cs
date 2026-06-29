using FastCart.Application.Auth;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Common.Interfaces;
using FastCart.Application.Profile;
using FastCart.Domain.Common;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Identity;

/// <summary>ASP.NET Identity-backed implementation of <see cref="IAuthService"/> (§6.1, §7).</summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly JwtTokenGenerator _tokens;
    private readonly IEmailSender _email;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        JwtTokenGenerator tokens,
        IEmailSender email)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _email = email;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new ValidationException(ToErrors(result));
        }

        await _userManager.AddToRoleAsync(user, Roles.Customer);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(request.Login);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedException("Invalid credentials.");
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (stored is null || !stored.IsActive)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Rotate: revoke the presented token, then issue a fresh pair.
        stored.RevokedAt = DateTime.UtcNow;

        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user is null)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task LogoutAsync(string userId, RefreshRequest request, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == userId, ct);

        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        // Anti-enumeration: always succeed, whether or not the email exists (§4.4).
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _email.SendAsync(
                request.Email,
                "Reset your FastCart password",
                $"Use this token to reset your password: {token}",
                ct);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new BusinessRuleException("Invalid password reset request.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(ToErrors(result));
        }

        // Reset invalidates outstanding refresh tokens.
        await RevokeAllAsync(user.Id, ct);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(ToErrors(result));
        }

        await RevokeAllAsync(user.Id, ct);
    }

    public async Task<MeResponse> GetMeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        var profile = await _db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        return new MeResponse(
            user.Id,
            user.UserName!,
            user.Email,
            user.PhoneNumber,
            roles.ToList(),
            user.IsTelegramLinked,
            profile is null
                ? null
                : new ProfileDto(profile.FirstName, profile.LastName, user.Email, user.PhoneNumber, profile.Dob, profile.ImageUrl));
    }

    private async Task<ApplicationUser?> ResolveUserAsync(string login)
    {
        var byEmail = await _userManager.FindByEmailAsync(login);
        if (byEmail is not null)
        {
            return byEmail;
        }

        var byName = await _userManager.FindByNameAsync(login);
        if (byName is not null)
        {
            return byName;
        }

        return await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == login);
    }

    private async Task<AuthResult> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = _tokens.CreateAccessToken(user.Id, user.UserName!, user.Email, roles);
        var refreshToken = _tokens.CreateRefreshToken();

        // Prune this user's dead refresh tokens (revoked or expired) so the table stays lean (§8).
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && (t.RevokedAt != null || t.ExpiresAt < now))
            .ExecuteDeleteAsync(ct);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshTokenDays)
        });
        await _db.SaveChangesAsync(ct);

        var user_ = new AuthUserDto(user.Id, user.UserName!, user.Email, user.PhoneNumber, roles.ToList(), user.IsTelegramLinked);
        return new AuthResult(accessToken, refreshToken, expiresAt, user_);
    }

    private async Task RevokeAllAsync(string userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in active)
        {
            token.RevokedAt = now;
        }

        if (active.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static IReadOnlyDictionary<string, string[]> ToErrors(IdentityResult result) =>
        new Dictionary<string, string[]>
        {
            ["identity"] = result.Errors.Select(e => e.Description).ToArray()
        };
}
