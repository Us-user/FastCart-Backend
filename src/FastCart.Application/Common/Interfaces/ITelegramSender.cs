namespace FastCart.Application.Common.Interfaces;

/// <summary>
/// Outbound Telegram abstraction (§4.4) — mirrors <see cref="IEmailSender"/>. The backend
/// holds the bot token and pushes messages directly via the Bot API. A dev implementation
/// logs instead of sending; the real implementation talks to Telegram.
/// </summary>
public interface ITelegramSender
{
    /// <summary>
    /// Sends <paramref name="text"/> to a Telegram chat. Returns <c>true</c> when delivered,
    /// <c>false</c> when the message could not be delivered (e.g. the user blocked the bot) —
    /// callers use this to fall back or stay neutral rather than surfacing an error.
    /// </summary>
    Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct = default);
}
