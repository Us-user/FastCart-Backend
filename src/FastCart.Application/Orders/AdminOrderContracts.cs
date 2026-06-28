using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;
using FastCart.Domain.Enums;

namespace FastCart.Application.Orders;

/// <summary>
/// Admin order management (§6.11). Drives the lifecycle through explicit actions —
/// confirm / reject / deliver / approve-return / decline-return — and restores stock
/// whenever an order is rejected or returned (§7.4).
/// </summary>
public interface IAdminOrderService
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(AdminOrderQuery query, CancellationToken ct = default);
    Task<OrderDto> GetAsync(int id, CancellationToken ct = default);
    Task<OrderDto> CreateOfflineAsync(AdminCreateOrderRequest request, CancellationToken ct = default);

    /// <summary>AwaitingConfirmation → InTransit.</summary>
    Task<OrderDto> ConfirmAsync(int id, CancellationToken ct = default);
    /// <summary>AwaitingConfirmation → Rejected (restores stock).</summary>
    Task<OrderDto> RejectAsync(int id, RejectOrderRequest request, CancellationToken ct = default);
    /// <summary>InTransit → Delivered.</summary>
    Task<OrderDto> MarkDeliveredAsync(int id, CancellationToken ct = default);
    /// <summary>ReturnRequested → Returned (restores stock).</summary>
    Task<OrderDto> ApproveReturnAsync(int id, CancellationToken ct = default);
    /// <summary>ReturnRequested → back to the status the order had before the return was requested.</summary>
    Task<OrderDto> DeclineReturnAsync(int id, CancellationToken ct = default);
}

/// <summary>Filter/sort/page for the admin order list (§6.11).</summary>
public sealed record AdminOrderQuery
{
    /// <summary>Filter by lifecycle status (e.g. <c>AwaitingConfirmation</c>, <c>ReturnRequested</c>).</summary>
    public OrderStatus? Status { get; init; }

    /// <summary>Free-text match against order number, customer name or email.</summary>
    public string? Q { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }

    /// <summary><c>newest</c> (default) | <c>oldest</c> | <c>total_desc</c> | <c>total_asc</c>.</summary>
    public string? Sort { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record AdminOrderItemInput
{
    [Required] public int ProductVariantId { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; } = 1;
}

/// <summary>Manual/offline order ("Add order", §6.11). UserId is null; items reference variants.</summary>
public sealed record AdminCreateOrderRequest
{
    [Required, StringLength(200)] public string CustomerName { get; init; } = default!;
    [Required, EmailAddress] public string CustomerEmail { get; init; } = default!;

    [Required, MinLength(1)] public List<AdminOrderItemInput> Items { get; init; } = new();

    [Required] public CheckoutAddressInput ShippingAddress { get; init; } = default!;
    public CheckoutAddressInput? BillingAddress { get; init; }

    /// <summary>How the customer paid/will pay (informational only).</summary>
    [Required] public PaymentMethod PaymentMethod { get; init; }

    [StringLength(500)] public string? CustomerNote { get; init; }
}

/// <summary>Reject an order awaiting confirmation (§7.2). Reason is optional but recommended.</summary>
public sealed record RejectOrderRequest
{
    [StringLength(300)] public string? Reason { get; init; }
}
