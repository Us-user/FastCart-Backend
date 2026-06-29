using FastCart.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace FastCart.Infrastructure.Messaging;

/// <summary>
/// Real Telegram sender (§4.4). Pushes messages straight from the backend with the same
/// <c>TELEGRAM_BOT_TOKEN</c> the bot uses. A blocked bot / inaccessible chat surfaces as a
/// 403 <see cref="ApiRequestException"/>; we translate that (and any transport error) into a
/// non-delivered result rather than throwing, so the reset flow stays neutral.
/// </summary>
public sealed class TelegramBotSender : ITelegramSender
{
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<TelegramBotSender> _logger;

    public TelegramBotSender(TelegramOptions options, ILogger<TelegramBotSender> logger)
    {
        _bot = new TelegramBotClient(options.BotToken!);
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct = default)
    {
        try
        {
            await _bot.SendMessage(chatId, text, cancellationToken: ct);
            return true;
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 403)
        {
            // User blocked the bot or never started a chat — expected, not an error.
            _logger.LogInformation("Telegram delivery skipped for chat {ChatId}: {Reason}", chatId, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram delivery failed for chat {ChatId}.", chatId);
            return false;
        }
    }
}
