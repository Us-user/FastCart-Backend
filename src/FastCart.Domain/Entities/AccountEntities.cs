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
