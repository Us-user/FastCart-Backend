using FastCart.Domain.Common;
using FastCart.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace FastCart.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity user (string/GUID key, §4.3/§5.1). Kept in Infrastructure so the
/// Domain stays free of the Identity dependency. Login accepts email or phone (§5.1);
/// that resolution is handled by the auth service in Phase 2.
/// </summary>
public class ApplicationUser : IdentityUser, IAuditable
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Telegram link (password-reset channel). Nullable until the user links their account;
    // ChatId is unique so a Telegram chat maps to at most one FastCart account.
    public long? TelegramChatId { get; set; }
    public long? TelegramUserId { get; set; }
    public DateTime? TelegramLinkedAt { get; set; }

    /// <summary>True once the account is linked to a Telegram chat (drives the "link required" prompt).</summary>
    public bool IsTelegramLinked => TelegramChatId is not null;

    public UserProfile? Profile { get; set; }
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

/// <summary>ASP.NET Identity role — seeded as Customer / Admin (§4.4).</summary>
public class ApplicationRole : IdentityRole
{
    public ApplicationRole() { }

    public ApplicationRole(string name) : base(name) { }
}
