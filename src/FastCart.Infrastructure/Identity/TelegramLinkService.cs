using FastCart.Application.Common.Exceptions;
using FastCart.Application.Telegram;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Messaging;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FastCart.Infrastructure.Identity;

/// <summary>EF Core implementation of <see cref="ITelegramLinkService"/> (§4.4).</summary>
public sealed class TelegramLinkService : ITelegramLinkService
{
    private readonly AppDbContext _db;
    private readonly TelegramOptions _options;
    private readonly int _tokenTtlMinutes;

    public TelegramLinkService(AppDbContext db, TelegramOptions options, IConfiguration config)
    {
        _db = db;
        _options = options;
        _tokenTtlMinutes = config.GetValue("Telegram:LinkTokenMinutes", 10);
    }

    public async Task<TelegramLinkStartResponse> StartAsync(string userId, CancellationToken ct = default)
    {
        // One outstanding token per user — drop any previous ones (used or not).
        await _db.TelegramLinkTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

        var token = SecretTokens.NewUrlToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(_tokenTtlMinutes);

        _db.TelegramLinkTokens.Add(new TelegramLinkToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt
        });
        await _db.SaveChangesAsync(ct);

        return new TelegramLinkStartResponse(_options.BuildDeepLink(token), expiresAt);
    }

    public async Task<TelegramLinkCompleteResponse> CompleteAsync(
        TelegramLinkCompleteRequest request, string? providedSecret, CancellationToken ct = default)
    {
        // Bot-only endpoint: require the shared secret (secure-by-default — reject if unset).
        if (string.IsNullOrWhiteSpace(_options.LinkSharedSecret) ||
            !SecretTokens.FixedTimeEquals(providedSecret, _options.LinkSharedSecret))
        {
            throw new UnauthorizedException("Invalid shared secret.");
        }

        var link = await _db.TelegramLinkTokens.FirstOrDefaultAsync(t => t.Token == request.Token, ct);
        if (link is null || !link.IsActive)
        {
            throw new BusinessRuleException("Invalid or expired link token.");
        }

        // A Telegram chat maps to at most one account.
        var chatTaken = await _db.Users
            .AnyAsync(u => u.TelegramChatId == request.ChatId && u.Id != link.UserId, ct);
        if (chatTaken)
        {
            throw new ConflictException("This Telegram account is already linked to another user.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == link.UserId, ct)
            ?? throw new NotFoundException("User not found.");

        user.TelegramChatId = request.ChatId;
        user.TelegramUserId = request.TelegramUserId;
        user.TelegramLinkedAt = DateTime.UtcNow;
        link.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new TelegramLinkCompleteResponse(true);
    }
}
