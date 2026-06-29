using Microsoft.Extensions.Configuration;

namespace FastCart.Infrastructure.Messaging;

/// <summary>
/// Telegram integration config (§9.4). Read from the <c>Telegram</c> section, each value
/// falling back to the conventional bare environment variable (<c>TELEGRAM_BOT_TOKEN</c>,
/// <c>TELEGRAM_BOT_USERNAME</c>, <c>TELEGRAM_LINK_SHARED_SECRET</c>) used by Render/containers.
/// </summary>
public sealed class TelegramOptions
{
    /// <summary>Bot API token; backend sends reset codes with it. Empty in dev → logging sender.</summary>
    public string? BotToken { get; init; }

    /// <summary>Bot @username (without the @), used to build the link deep-link.</summary>
    public string? BotUsername { get; init; }

    /// <summary>Shared secret the bot must present on <c>/telegram/link/complete</c>.</summary>
    public string? LinkSharedSecret { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);

    /// <summary>Builds the <c>https://t.me/&lt;bot&gt;?start=&lt;token&gt;</c> deep-link for linking.</summary>
    public string BuildDeepLink(string token) =>
        $"https://t.me/{BotUsername}?start={token}";

    public static TelegramOptions FromConfiguration(IConfiguration config)
    {
        var section = config.GetSection("Telegram");
        return new TelegramOptions
        {
            BotToken = First(section["BotToken"], config["TELEGRAM_BOT_TOKEN"]),
            BotUsername = First(section["BotUsername"], config["TELEGRAM_BOT_USERNAME"]),
            LinkSharedSecret = First(section["LinkSharedSecret"], config["TELEGRAM_LINK_SHARED_SECRET"])
        };
    }

    private static string? First(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : fallback;
}
