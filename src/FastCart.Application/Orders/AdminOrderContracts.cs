using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;
using FastCart.Domain.Enums;

namespace FastCart.Application.Orders;

/// <summary>Admin order &amp; return management (§6.11, §7.2, §7.4).</summary>
public interface IAdminOrderService
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(AdminOrderQuery query, CancellationToken ct = default);
    Task<OrderDto> GetAsync(int id, CancellationToken ct = default);
    Task<OrderDto> CreateOfflineAsync(AdminCreateOrderRequest request, CancellationToken ct = default);
    Task<OrderDto> SetStatusAsync(int id, SetOrderStatusRequest request, CancellationToken ct = default);
    Task<OrderDto> SetPaymentStatusAsync(int id, SetPaymentStatusRequest request, CancellationToken ct = default);

    Task<PagedResult<AdminReturnDto>> ListReturnsAsync(ReturnStatus? status, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<AdminReturnDto> ResolveReturnAsync(int id, ResolveReturnRequest request, CancellationToken ct = default);
}

/// <summary>Filter/sort/page for the admin order list (§6.11).</summary>
public sealed record AdminOrderQuery
{
    public OrderStatus? Status { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }

    /// <summary>Matches order number, customer name or email.</summary>
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

    [Required] public PaymentMethod PaymentMethod { get; init; }

    /// <summary>Optional initial payment status (e.g. an already-paid offline order). Defaults to Pending.</summary>
    public PaymentStatus? PaymentStatus { get; init; }

    [StringLength(500)] public string? CustomerNote { get; init; }
}

public sealed record SetOrderStatusRequest
{
    [Required] public OrderStatus Status { get; init; }
    [StringLength(300)] public string? Reason { get; init; }
}

public sealed record SetPaymentStatusRequest
{
    [Required] public PaymentStatus PaymentStatus { get; init; }
}

public sealed record ResolveReturnRequest
{
    /// <summary>Target state: <c>Approved</c>, <c>Rejected</c> or <c>Completed</c>.</summary>
    [Required] public ReturnStatus Status { get; init; }
}

public sealed record AdminReturnDto(
    int Id,
    int OrderId,
    string OrderNumber,
    string? UserId,
    string CustomerName,
    string Reason,
    ReturnStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
