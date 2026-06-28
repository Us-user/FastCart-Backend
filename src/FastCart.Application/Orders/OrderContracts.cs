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
    Task<OrderDto> RequestReturnAsync(string userId, int id, CreateReturnRequest request, CancellationToken ct = default);
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

/// <summary>
/// Place an order from the current cart (§7.1). Provide the shipping address either by
/// <see cref="ShippingAddressId"/> (from the address book) or inline via <see cref="ShippingAddress"/>;
/// billing is optional and defaults to shipping. The order is created as
/// <c>AwaitingConfirmation</c> and stock is reserved immediately. Prices, the coupon discount
/// and stock are always recomputed server-side — values from the client are ignored.
/// </summary>
public sealed record CheckoutRequest
{
    /// <summary>Use a saved address by id, or omit and pass <see cref="ShippingAddress"/> inline.</summary>
    public int? ShippingAddressId { get; init; }
    public CheckoutAddressInput? ShippingAddress { get; init; }
    public CheckoutAddressInput? BillingAddress { get; init; }

    /// <summary>When true and an inline shipping address was given, also save it to the address book.</summary>
    public bool SaveAddress { get; init; }

    /// <summary>How the customer intends to pay. Informational — payment is verified manually by an admin.</summary>
    [Required] public PaymentMethod PaymentMethod { get; init; }

    [StringLength(64)] public string? CouponCode { get; init; }
    [StringLength(500)] public string? CustomerNote { get; init; }
}

/// <summary>Cancel an order that is still <c>AwaitingConfirmation</c> (§7.4). Reason is optional.</summary>
public sealed record CancelOrderRequest
{
    [StringLength(300)] public string? Reason { get; init; }
}

/// <summary>Request a return for an <c>InTransit</c> or <c>Delivered</c> order (§7.4).</summary>
public sealed record CreateReturnRequest
{
    [Required, StringLength(500)] public string Reason { get; init; } = default!;
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

/// <summary>Full order detail, including the current lifecycle status and its timestamps.</summary>
public sealed record OrderDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
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
    DateTime? ConfirmedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancelReason,
    DateTime? RejectedAt,
    string? RejectReason,
    DateTime? ReturnRequestedAt,
    string? ReturnReason,
    DateTime? ReturnedAt,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);

/// <summary>Compact order row for lists (customer "My orders" and the admin order table).</summary>
public sealed record OrderSummaryDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    decimal Total,
    int ItemCount,
    DateTime CreatedAt);
