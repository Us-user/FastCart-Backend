using FastCart.Domain.Common;
using FastCart.Domain.Enums;

namespace FastCart.Domain.Entities;

/// <summary>One active cart per user (§5.3).</summary>
public class Cart : BaseEntity
{
    public string UserId { get; set; } = default!;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

/// <summary>Cart line — references the chosen variant (§5.3, §6.7).</summary>
public class CartItem : BaseEntity
{
    public int CartId { get; set; }
    public Cart Cart { get; set; } = default!;
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;
    public int Quantity { get; set; }
}

/// <summary>Product-level wishlist entry (the heart on a card), unique per user+product (§5.3).</summary>
public class WishlistItem : BaseEntity
{
    public string UserId { get; set; } = default!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
}

/// <summary>
/// Product review (§6.6). Implied by the API though not tabulated in §5; any
/// authenticated customer may post (D4), admins may delete.
/// </summary>
public class Review : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Discount coupon (§5.4, §7.5).</summary>
public class Coupon : BaseEntity
{
    public string Code { get; set; } = default!;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }

    /// <summary>Caps a percentage discount (§5.4).</summary>
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? UsageLimit { get; set; }
    public int? PerUserLimit { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CouponRedemption> Redemptions { get; set; } = new List<CouponRedemption>();
}

/// <summary>Records a coupon use, enforcing per-user limits (§5.4).</summary>
public class CouponRedemption : BaseEntity
{
    public int CouponId { get; set; }
    public Coupon Coupon { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public int OrderId { get; set; }
    public DateTime UsedAt { get; set; }
}
