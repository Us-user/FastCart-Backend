using FastCart.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace FastCart.Infrastructure.Messaging;

/// <summary>
/// Development Telegram sender — logs the message (including reset codes) instead of calling
/// the Bot API (§4.4), used when no <c>TELEGRAM_BOT_TOKEN</c> is configured. Mirrors
/// <see cref="LoggingEmailSender"/>. Always reports delivered so the dev flow proceeds.
/// </summary>
public sealed class LoggingTelegramSender : ITelegramSender
{
    private readonly ILogger<LoggingTelegramSender> _logger;

    public LoggingTelegramSender(ILogger<LoggingTelegramSender> logger) => _logger = logger;

    public Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct = default)
    {
        _logger.LogInformation("DEV TELEGRAM → chat {ChatId}{NewLine}{Body}", chatId, Environment.NewLine, text);
        return Task.FromResult(true);
    }
}
