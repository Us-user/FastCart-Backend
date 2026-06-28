using FastCart.Domain.Common;
using FastCart.Domain.Enums;

namespace FastCart.Domain.Entities;

/// <summary>
/// Customer or admin-offline order with address + line snapshots (§5.5). The lifecycle is
/// driven entirely by <see cref="Status"/>; each transition stamps its own timestamp/reason
/// so the customer and admin can always see exactly where an order stands.
/// </summary>
public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = default!;

    /// <summary>Null for admin "offline" orders (§5.5).</summary>
    public string? UserId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;

    public OrderStatus Status { get; set; } = OrderStatus.AwaitingConfirmation;

    /// <summary>How the customer chose to pay. Informational only — payment is verified manually.</summary>
    public PaymentMethod PaymentMethod { get; set; }
    public string Currency { get; set; } = "USD";

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal Total { get; set; }
    public string? CustomerNote { get; set; }

    // ---- Lifecycle timestamps & reasons --------------------------------------
    /// <summary>When an admin confirmed the order (→ InTransit).</summary>
    public DateTime? ConfirmedAt { get; set; }
    /// <summary>When an admin marked the order delivered.</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>When the customer cancelled the order while awaiting confirmation.</summary>
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>When an admin rejected the order.</summary>
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>When the customer requested a return.</summary>
    public DateTime? ReturnRequestedAt { get; set; }
    public string? ReturnReason { get; set; }
    /// <summary>When an admin approved the return (→ Returned).</summary>
    public DateTime? ReturnedAt { get; set; }
    /// <summary>The status the order was in before a return was requested, so a declined return can restore it.</summary>
    public OrderStatus? StatusBeforeReturn { get; set; }

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

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
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
