using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;
using FastCart.Domain.Enums;

namespace FastCart.Application.Coupons;

/// <summary>Coupon validation (customer) + admin CRUD (§6.9, §7.5).</summary>
public interface ICouponService
{
    Task<CouponValidationResult> ValidateAsync(string userId, ValidateCouponRequest request, CancellationToken ct = default);
    Task<PagedResult<CouponDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<CouponDto> GetAsync(int id, CancellationToken ct = default);
    Task<CouponDto> CreateAsync(CouponRequest request, CancellationToken ct = default);
    Task<CouponDto> UpdateAsync(int id, CouponRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record CouponDto(
    int Id, string Code, DiscountType DiscountType, decimal DiscountValue,
    decimal? MinOrderAmount, decimal? MaxDiscountAmount, DateTime? StartsAt, DateTime? ExpiresAt,
    int? UsageLimit, int? PerUserLimit, int TimesUsed, bool IsActive);

public sealed record CouponValidationResult(bool IsValid, string Message, decimal DiscountAmount, decimal FinalTotal);

public sealed record ValidateCouponRequest
{
    [Required]
    public string Code { get; init; } = default!;

    [Range(0, double.MaxValue)]
    public decimal CartTotal { get; init; }
}

public sealed record CouponRequest
{
    [Required, StringLength(64)]
    public string Code { get; init; } = default!;

    [Required]
    public DiscountType DiscountType { get; init; }

    [Range(0, double.MaxValue)]
    public decimal DiscountValue { get; init; }

    public decimal? MinOrderAmount { get; init; }
    public decimal? MaxDiscountAmount { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int? UsageLimit { get; init; }
    public int? PerUserLimit { get; init; }
    public bool IsActive { get; init; } = true;
}
