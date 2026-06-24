using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;
using FastCart.Domain.Enums;

namespace FastCart.Application.Orders;

/// <summary>Checkout + customer-facing order operations (§6.10, §7.1). Scoped to the caller.</summary>
public interface IOrderService
{
    Task<OrderDto> CheckoutAsync(string userId, CheckoutRequest request, CancellationToken ct = default);
    Task<PagedResult<OrderSummaryDto>> ListMineAsync(string userId, OrderStatus? status, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<OrderDto> GetMineAsync(string userId, int id, CancellationToken ct = default);
    Task<OrderDto> CancelAsync(string userId, int id, CancelOrderRequest request, CancellationToken ct = default);
    Task<ReturnRequestDto> RequestReturnAsync(string userId, int id, CreateReturnRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ReturnRequestDto>> ListMyReturnsAsync(string userId, CancellationToken ct = default);
    Task<OrderDto> PayAsync(string userId, int id, PayOrderRequest request, CancellationToken ct = default);
}

// ---- Requests ----------------------------------------------------------------

/// <summary>Inline checkout address (§7.1). Used when no <c>shippingAddressId</c> is given.</summary>
public sealed record CheckoutAddressInput
{
    [Required, StringLength(100)] public string FirstName { get; init; } = default!;
    [Required, StringLength(100)] public string LastName { get; init; } = default!;
    [Required, StringLength(256)] public string StreetAddress { get; init; } = default!;
    [StringLength(100)] public string? Apartment { get; init; }
    [Required, StringLength(100)] public string City { get; init; } = default!;
    [Required, Phone] public string PhoneNumber { get; init; } = default!;
    [Required, EmailAddress] public string Email { get; init; } = default!;
}

/// <summary>Optional, non-sensitive payment details (§7.3, D3). No card number/CVV is stored.</summary>
public sealed record PaymentDetailsInput
{
    [StringLength(100)] public string? Reference { get; init; }
}

/// <summary>
/// Checkout body (§7.1). Provide the shipping address either by <see cref="ShippingAddressId"/>
/// (from the address book) or inline via <see cref="ShippingAddress"/>. Billing is optional and
/// defaults to the shipping address when omitted.
/// </summary>
public sealed record CheckoutRequest
{
    public int? ShippingAddressId { get; init; }
    public CheckoutAddressInput? ShippingAddress { get; init; }
    public CheckoutAddressInput? BillingAddress { get; init; }
    public bool SaveAddress { get; init; }

    [Required] public PaymentMethod PaymentMethod { get; init; }
    public PaymentDetailsInput? PaymentDetails { get; init; }

    [StringLength(64)] public string? CouponCode { get; init; }
    [StringLength(500)] public string? CustomerNote { get; init; }
}

public sealed record CancelOrderRequest
{
    [StringLength(300)] public string? Reason { get; init; }
}

public sealed record CreateReturnRequest
{
    [Required, StringLength(500)] public string Reason { get; init; } = default!;
}

/// <summary>Record a payment against an existing order (§6.10). Defaults to the order's method.</summary>
public sealed record PayOrderRequest
{
    public PaymentMethod? Method { get; init; }
    public PaymentDetailsInput? PaymentDetails { get; init; }
}

// ---- Responses ---------------------------------------------------------------

public sealed record AddressSnapshotDto(
    string FirstName,
    string LastName,
    string StreetAddress,
    string? Apartment,
    string City,
    string Phone,
    string Email);

public sealed record OrderItemDto(
    int Id,
    int? ProductId,
    int? ProductVariantId,
    string ProductName,
    string Sku,
    string? VariantDescription,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    PaymentMethod PaymentMethod,
    string Currency,
    decimal Subtotal,
    decimal DiscountAmount,
    string? CouponCode,
    decimal TaxAmount,
    decimal ShippingAmount,
    decimal Total,
    string CustomerName,
    string CustomerEmail,
    string? CustomerNote,
    AddressSnapshotDto ShippingAddress,
    AddressSnapshotDto? BillingAddress,
    DateTime? CancelledAt,
    string? CancelReason,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderSummaryDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    PaymentMethod PaymentMethod,
    decimal Total,
    int ItemCount,
    DateTime CreatedAt);

public sealed record ReturnRequestDto(
    int Id,
    int OrderId,
    string OrderNumber,
    string Reason,
    ReturnStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
