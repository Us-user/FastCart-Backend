using FastCart.Domain.Common;
using FastCart.Domain.Enums;

namespace FastCart.Domain.Entities;

/// <summary>Customer or admin-offline order with address snapshots (§5.5, §7.1).</summary>
public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = default!;

    /// <summary>Null for admin "offline" orders (§5.5).</summary>
    public string? UserId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;

    public OrderStatus Status { get; set; } = OrderStatus.New;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public string Currency { get; set; } = "USD";

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal Total { get; set; }
    public string? CustomerNote { get; set; }

    // Shipping address snapshot.
    public string ShipFirstName { get; set; } = default!;
    public string ShipLastName { get; set; } = default!;
    public string ShipStreetAddress { get; set; } = default!;
    public string? ShipApartment { get; set; }
    public string ShipCity { get; set; } = default!;
    public string ShipPhone { get; set; } = default!;
    public string ShipEmail { get; set; } = default!;

    // Billing address snapshot (nullable — defaults to shipping when omitted).
    public string? BillFirstName { get; set; }
    public string? BillLastName { get; set; }
    public string? BillStreetAddress { get; set; }
    public string? BillApartment { get; set; }
    public string? BillCity { get; set; }
    public string? BillPhone { get; set; }
    public string? BillEmail { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

/// <summary>
/// Order line with purchase-time snapshots so later catalog/variant edits never
/// rewrite history (§5.5). UnitCost is snapshotted for stable profit reporting (D9).
/// </summary>
public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public int? ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string? VariantDescription { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>Payment record behind the IPaymentProvider abstraction (§5.5, §7.3, D2/D3).</summary>
public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public PaymentMethod Method { get; set; }
    public string Provider { get; set; } = "Manual";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Non-sensitive reference only — no PAN/CVV is ever stored (D3).</summary>
    public string? Reference { get; set; }
    public DateTime? PaidAt { get; set; }
}

/// <summary>Lightweight return request (§5.5, §7.4).</summary>
public class ReturnRequest : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    public DateTime? ResolvedAt { get; set; }
}
