using System.ComponentModel.DataAnnotations;
using FastCart.Application.Profile;

namespace FastCart.Application.Auth;

/// <summary>Auth operations (§6.1). Implemented in Infrastructure over ASP.NET Identity.</summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(string userId, RefreshRequest request, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task<MeResponse> GetMeAsync(string userId, CancellationToken ct = default);
}

/// <summary>Registration payload — mirrors the reference contract (§8).</summary>
public sealed record RegisterRequest
{
    [Required, StringLength(64, MinimumLength = 3)]
    public string UserName { get; init; } = default!;

    [Required, EmailAddress]
    public string Email { get; init; } = default!;

    [Required, Phone]
    public string PhoneNumber { get; init; } = default!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = default!;

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; init; } = default!;
}

/// <summary>Login with email <i>or</i> phone (§6.1).</summary>
public sealed record LoginRequest
{
    [Required]
    public string Login { get; init; } = default!;

    [Required]
    public string Password { get; init; } = default!;
}

public sealed record RefreshRequest
{
    [Required]
    public string RefreshToken { get; init; } = default!;
}

public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = default!;
}

/// <summary>
/// Reset payload. <see cref="Email"/> is included alongside the Identity token so the
/// user can be resolved (the reset link carries both) — a small, documented addition
/// to the §6.1 body which lists only token + passwords.
/// </summary>
public sealed record ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = default!;

    [Required]
    public string Token { get; init; } = default!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = default!;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; init; } = default!;
}

public sealed record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = default!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = default!;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; init; } = default!;
}

/// <summary>Authenticated user summary returned with tokens.</summary>
public sealed record AuthUserDto(
    string Id,
    string UserName,
    string? Email,
    string? PhoneNumber,
    IReadOnlyList<string> Roles);

/// <summary>Access + rotating refresh token pair (§4.4).</summary>
public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AuthUserDto User)
{
    public string TokenType => "Bearer";
}

/// <summary>Current user + profile + roles (§6.1 /auth/me).</summary>
public sealed record MeResponse(
    string Id,
    string UserName,
    string? Email,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    ProfileDto? Profile);
