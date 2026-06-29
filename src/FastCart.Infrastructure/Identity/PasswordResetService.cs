using FastCart.Application.Auth;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Common.Interfaces;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FastCart.Infrastructure.Identity;

/// <summary>
/// Telegram-delivered password reset (§4.4). Codes are stored only as keyed hashes, are
/// single-use with a short TTL, and are bounded by an attempt limit and a per-account resend
/// cooldown. The request step is strictly anti-enumeration; verify/confirm return generic
/// failures. The new password is applied through the existing Identity hasher — unchanged.
/// </summary>
public sealed class PasswordResetService : IPasswordResetService
{
    // Generic, non-revealing failure for verify/confirm.
    private const string InvalidCode = "Invalid or expired code.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITelegramSender _telegram;
    private readonly string _pepper;
    private readonly int _codeTtlMinutes;
    private readonly int _changeTtlMinutes;
    private readonly int _maxAttempts;
    private readonly int _resendCooldownSeconds;

    public PasswordResetService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ITelegramSender telegram,
        IConfiguration config)
    {
        _userManager = userManager;
        _db = db;
        _telegram = telegram;
        // Pepper for the low-entropy code hash; falls back to the JWT signing key.
        _pepper = config["Telegram:CodePepper"] ?? config["Jwt:Secret"] ?? "fastcart-reset-pepper";
        _codeTtlMinutes = config.GetValue("Telegram:ResetCodeMinutes", 10);
        _changeTtlMinutes = config.GetValue("Telegram:ResetChangeTokenMinutes", 10);
        _maxAttempts = config.GetValue("Telegram:ResetMaxAttempts", 5);
        _resendCooldownSeconds = config.GetValue("Telegram:ResetResendCooldownSeconds", 60);
    }

    public async Task RequestAsync(TelegramResetRequestRequest request, CancellationToken ct = default)
    {
        // Always neutral: never reveal whether the account exists or has Telegram linked.
        var user = await ResolveUserAsync(request.Login);
        if (user?.TelegramChatId is not long chatId)
        {
            return;
        }

        // Per-account throttle: ignore requests that arrive within the resend cooldown.
        var cutoff = DateTime.UtcNow.AddSeconds(-_resendCooldownSeconds);
        var issuedRecently = await _db.PasswordResetCodes
            .AnyAsync(c => c.UserId == user.Id && c.CreatedAt > cutoff, ct);
        if (issuedRecently)
        {
            return;
        }

        // Invalidate any previously outstanding (unverified) codes for this user.
        await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.ConsumedAt == null && c.ChangeTokenHash == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsumedAt, DateTime.UtcNow), ct);

        var code = SecretTokens.NewNumericCode();
        _db.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = SecretTokens.Hmac(code, _pepper),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_codeTtlMinutes),
            Attempts = 0
        });
        await _db.SaveChangesAsync(ct);

        var text =
            $"FastCart: your password reset code is {code}. " +
            $"It expires in {_codeTtlMinutes} minutes. If you didn't request this, ignore this message.";

        // Delivery failures (e.g. the user blocked the bot) stay neutral — no fallback, no error.
        await _telegram.SendMessageAsync(chatId, text, ct);
    }

    public async Task<TelegramResetVerifyResponse> VerifyAsync(
        TelegramResetVerifyRequest request, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(request.Login)
            ?? throw new BusinessRuleException(InvalidCode);

        // Latest outstanding, not-yet-verified code for this user.
        var record = await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.ConsumedAt == null && c.ChangeTokenHash == null)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (record is null || record.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessRuleException(InvalidCode);
        }

        if (record.Attempts >= _maxAttempts)
        {
            record.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException("Too many attempts. Request a new code.");
        }

        var matches = SecretTokens.FixedTimeEquals(
            SecretTokens.Hmac(request.Code, _pepper), record.CodeHash);
        if (!matches)
        {
            record.Attempts++;
            if (record.Attempts >= _maxAttempts)
            {
                record.ConsumedAt = DateTime.UtcNow; // burn the code once attempts are exhausted.
            }
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException(InvalidCode);
        }

        // Success: mint a single-use change token (stored as a hash) and leave the row open
        // until confirm. The presence of ChangeTokenHash blocks re-verification of this code.
        var changeToken = SecretTokens.NewUrlToken();
        record.ChangeTokenHash = SecretTokens.Sha256(changeToken);
        record.ChangeTokenExpiresAt = DateTime.UtcNow.AddMinutes(_changeTtlMinutes);
        await _db.SaveChangesAsync(ct);

        return new TelegramResetVerifyResponse(changeToken, record.ChangeTokenExpiresAt.Value);
    }

    public async Task ConfirmAsync(TelegramResetConfirmRequest request, CancellationToken ct = default)
    {
        var hash = SecretTokens.Sha256(request.ChangeToken);
        var record = await _db.PasswordResetCodes
            .FirstOrDefaultAsync(c => c.ChangeTokenHash == hash && c.ConsumedAt == null, ct);

        if (record?.ChangeTokenExpiresAt is null || record.ChangeTokenExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessRuleException("Invalid or expired reset session.");
        }

        var user = await _userManager.FindByIdAsync(record.UserId)
            ?? throw new NotFoundException("User not found.");

        // Apply the new password with the existing Identity hasher (unchanged mechanism).
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(ToErrors(result));
        }

        // Burn the change token and revoke active sessions.
        record.ConsumedAt = DateTime.UtcNow;
        await RevokeRefreshTokensAsync(user.Id, ct);
        await _db.SaveChangesAsync(ct);
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

    private async Task RevokeRefreshTokensAsync(string userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private static IReadOnlyDictionary<string, string[]> ToErrors(IdentityResult result) =>
        new Dictionary<string, string[]>
        {
            ["identity"] = result.Errors.Select(e => e.Description).ToArray()
        };
}
