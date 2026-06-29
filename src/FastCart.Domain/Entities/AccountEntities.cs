using FastCart.Domain.Common;

namespace FastCart.Domain.Entities;

/// <summary>
/// 1:1 with the Identity user (§5.1). The user itself lives in Infrastructure
/// (ASP.NET Identity); profiles, addresses and tokens are referenced by string UserId.
/// </summary>
public class UserProfile : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public DateOnly? Dob { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Long-lived, rotating, revocable refresh token (§4.4, §5.1).</summary>
public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}

/// <summary>
/// One-time token for linking a Telegram account. Issued for an authenticated user,
/// carried in the bot deep-link (<c>?start=&lt;token&gt;</c>) and redeemed by the bot.
/// Single-use (<see cref="UsedAt"/>) with a short TTL (<see cref="ExpiresAt"/>).
/// </summary>
public class TelegramLinkToken : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsActive => UsedAt is null && DateTime.UtcNow < ExpiresAt;
}

/// <summary>
/// Single-use password-reset code delivered over Telegram (§4.4). Only the HMAC of the
/// 6-digit code is stored (<see cref="CodeHash"/>), never the code itself. On successful
/// verification a short-lived change token is minted (its hash kept here) and consumed at
/// confirm. Attempts are bounded; exceeding the limit invalidates the code.
/// </summary>
public class PasswordResetCode : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string CodeHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public int Attempts { get; set; }

    // Set only after the code is verified — the credential handed back to the client and
    // exchanged at /auth/reset/confirm. Stored as a hash; never round-tripped in plaintext.
    public string? ChangeTokenHash { get; set; }
    public DateTime? ChangeTokenExpiresAt { get; set; }

    public bool IsActive => ConsumedAt is null && DateTime.UtcNow < ExpiresAt;
}

/// <summary>Address-book entry (§5.1, §6.3). Snapshotted onto orders at checkout.</summary>
public class Address : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string StreetAddress { get; set; } = default!;
    public string? Apartment { get; set; }
    public string City { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string Email { get; set; } = default!;
    public bool IsDefault { get; set; }
}
