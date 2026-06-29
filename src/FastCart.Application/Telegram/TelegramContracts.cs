using System.ComponentModel.DataAnnotations;

namespace FastCart.Application.Telegram;

/// <summary>
/// Telegram account-linking (§4.4). The user starts linking while authenticated; the bot
/// completes it out-of-band by redeeming the one-time token together with the chat id.
/// Implemented in Infrastructure over EF Core + the bot config.
/// </summary>
public interface ITelegramLinkService
{
    /// <summary>Issues a one-time link token for the authenticated user and returns the deep-link.</summary>
    Task<TelegramLinkStartResponse> StartAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Bot-only. Validates the shared secret and the token, then attaches the chat id to the
    /// user. <paramref name="providedSecret"/> is the secret the bot sent in the request header.
    /// </summary>
    Task<TelegramLinkCompleteResponse> CompleteAsync(
        TelegramLinkCompleteRequest request, string? providedSecret, CancellationToken ct = default);
}

/// <summary>Deep-link the client opens to launch the bot: <c>https://t.me/&lt;bot&gt;?start=&lt;token&gt;</c>.</summary>
public sealed record TelegramLinkStartResponse(string DeepLink, DateTime ExpiresAt);

/// <summary>Payload the bot posts back to <c>/telegram/link/complete</c>.</summary>
public sealed record TelegramLinkCompleteRequest
{
    [Required]
    public string Token { get; init; } = default!;

    /// <summary>Telegram chat id the reset codes will be sent to.</summary>
    [Required]
    public long ChatId { get; init; }

    /// <summary>Telegram user id (optional — stored for reference).</summary>
    public long? TelegramUserId { get; init; }
}

public sealed record TelegramLinkCompleteResponse(bool Linked);
