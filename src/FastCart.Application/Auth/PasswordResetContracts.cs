using System.ComponentModel.DataAnnotations;

namespace FastCart.Application.Auth;

/// <summary>
/// Telegram-delivered password reset (§4.4). A three-step flow: request a code, verify it
/// (which mints a short-lived change token), then confirm the new password with that token.
/// Sits alongside — and does not replace — the existing email-token reset.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>Sends a reset code over Telegram if the account is linked. Always neutral.</summary>
    Task RequestAsync(TelegramResetRequestRequest request, CancellationToken ct = default);

    /// <summary>Verifies the code; on success returns a short-lived change token.</summary>
    Task<TelegramResetVerifyResponse> VerifyAsync(TelegramResetVerifyRequest request, CancellationToken ct = default);

    /// <summary>Exchanges the change token for the new password; invalidates code + sessions.</summary>
    Task ConfirmAsync(TelegramResetConfirmRequest request, CancellationToken ct = default);
}

/// <summary>Step 1 — identify the account by email, username or phone.</summary>
public sealed record TelegramResetRequestRequest
{
    [Required]
    public string Login { get; init; } = default!;
}

/// <summary>Step 2 — the account identifier plus the 6-digit code received in Telegram.</summary>
public sealed record TelegramResetVerifyRequest
{
    [Required]
    public string Login { get; init; } = default!;

    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; init; } = default!;
}

/// <summary>Short-lived token authorizing a single password change.</summary>
public sealed record TelegramResetVerifyResponse(string ChangeToken, DateTime ExpiresAt);

/// <summary>Step 3 — the change token from verify and the new password.</summary>
public sealed record TelegramResetConfirmRequest
{
    [Required]
    public string ChangeToken { get; init; } = default!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = default!;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; init; } = default!;
}
