using System.Globalization;
using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Coupons;
using FastCart.Domain.Entities;
using FastCart.Domain.Enums;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Commerce;

/// <summary>Coupon validation + admin CRUD (§6.9). Rule engine per §7.5, reused at checkout.</summary>
public sealed class CouponService : ICouponService
{
    private readonly AppDbContext _db;

    public CouponService(AppDbContext db) => _db = db;

    public async Task<CouponValidationResult> ValidateAsync(string userId, ValidateCouponRequest request, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == request.Code, ct);
        if (coupon is null)
        {
            return new CouponValidationResult(false, "Invalid coupon code.", 0, request.CartTotal);
        }

        var userRedemptions = await _db.CouponRedemptions
            .CountAsync(r => r.CouponId == coupon.Id && r.UserId == userId, ct);

        var (valid, message, discount) = Evaluate(coupon, request.CartTotal, userRedemptions);
        return new CouponValidationResult(valid, message, discount, request.CartTotal - discount);
    }

    /// <summary>
    /// Authoritative coupon rules (§7.5): active, in-window, meets MinOrderAmount, within
    /// total and per-user usage limits; percentage discounts respect MaxDiscountAmount.
    /// Reused by checkout (Phase 5).
    /// </summary>
    public static (bool Valid, string Message, decimal Discount) Evaluate(Coupon coupon, decimal cartTotal, int userRedemptions)
    {
        var now = DateTime.UtcNow;

        if (!coupon.IsActive) return (false, "Coupon is not active.", 0m);
        if (coupon.StartsAt is not null && now < coupon.StartsAt) return (false, "Coupon is not yet valid.", 0m);
        if (coupon.ExpiresAt is not null && now > coupon.ExpiresAt) return (false, "Coupon has expired.", 0m);
        if (coupon.MinOrderAmount is not null && cartTotal < coupon.MinOrderAmount)
            return (false, $"Order total must be at least {coupon.MinOrderAmount.Value.ToString("0.00", CultureInfo.InvariantCulture)}.", 0m);
        if (coupon.UsageLimit is not null && coupon.TimesUsed >= coupon.UsageLimit)
            return (false, "Coupon usage limit reached.", 0m);
        if (coupon.PerUserLimit is not null && userRedemptions >= coupon.PerUserLimit)
            return (false, "You have already used this coupon.", 0m);

        var discount = coupon.DiscountType == DiscountType.Percentage
            ? cartTotal * coupon.DiscountValue / 100m
            : coupon.DiscountValue;

        if (coupon.DiscountType == DiscountType.Percentage && coupon.MaxDiscountAmount is not null && discount > coupon.MaxDiscountAmount)
        {
            discount = coupon.MaxDiscountAmount.Value;
        }
        if (discount > cartTotal) discount = cartTotal;

        return (true, "Coupon applied.", Math.Round(discount, 2));
    }

    public async Task<PagedResult<CouponDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var page = pageNumber < 1 ? 1 : pageNumber;
        var size = pageSize is < 1 or > 100 ? 20 : pageSize;

        var total = await _db.Coupons.CountAsync(ct);
        var rows = await _db.Coupons.AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        var items = rows.Select(Map).ToList();

        return new PagedResult<CouponDto>(items, page, size, total);
    }

    public async Task<CouponDto> GetAsync(int id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Coupon not found.");
        return Map(coupon);
    }

    public async Task<CouponDto> CreateAsync(CouponRequest request, CancellationToken ct = default)
    {
        if (await _db.Coupons.AnyAsync(c => c.Code == request.Code, ct))
        {
            throw new ConflictException("A coupon with this code already exists.");
        }

        var coupon = new Coupon();
        Apply(coupon, request);
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);
        return Map(coupon);
    }

    public async Task<CouponDto> UpdateAsync(int id, CouponRequest request, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Coupon not found.");

        if (!string.Equals(coupon.Code, request.Code, StringComparison.OrdinalIgnoreCase) &&
            await _db.Coupons.AnyAsync(c => c.Code == request.Code && c.Id != id, ct))
        {
            throw new ConflictException("A coupon with this code already exists.");
        }

        Apply(coupon, request);
        await _db.SaveChangesAsync(ct);
        return Map(coupon);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Coupon not found.");
        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(Coupon coupon, CouponRequest r)
    {
        coupon.Code = r.Code;
        coupon.DiscountType = r.DiscountType;
        coupon.DiscountValue = r.DiscountValue;
        coupon.MinOrderAmount = r.MinOrderAmount;
        coupon.MaxDiscountAmount = r.MaxDiscountAmount;
        coupon.StartsAt = r.StartsAt;
        coupon.ExpiresAt = r.ExpiresAt;
        coupon.UsageLimit = r.UsageLimit;
        coupon.PerUserLimit = r.PerUserLimit;
        coupon.IsActive = r.IsActive;
    }

    private static CouponDto Map(Coupon c) => new(
        c.Id, c.Code, c.DiscountType, c.DiscountValue, c.MinOrderAmount, c.MaxDiscountAmount,
        c.StartsAt, c.ExpiresAt, c.UsageLimit, c.PerUserLimit, c.TimesUsed, c.IsActive);
}
