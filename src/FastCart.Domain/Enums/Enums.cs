namespace FastCart.Domain.Enums;

/// <summary>
/// Order lifecycle. The customer places an order (<see cref="AwaitingConfirmation"/>); an admin
/// either confirms it (→ <see cref="InTransit"/>) or rejects it (→ <see cref="Rejected"/>), then
/// marks it <see cref="Delivered"/>. The customer may cancel while still awaiting confirmation
/// (→ <see cref="Cancelled"/>) or request a return once it is in transit/delivered
/// (→ <see cref="ReturnRequested"/> → <see cref="Returned"/> on admin approval).
/// </summary>
public enum OrderStatus
{
    /// <summary>Just placed by the customer; waiting for an admin to confirm or reject.</summary>
    AwaitingConfirmation,

    /// <summary>Admin confirmed the order; it is on its way to the customer.</summary>
    InTransit,

    /// <summary>Admin marked the order as delivered to the customer.</summary>
    Delivered,

    /// <summary>Customer cancelled the order before it was confirmed (stock restored).</summary>
    Cancelled,

    /// <summary>Admin declined the order, e.g. price/stock issue (stock restored).</summary>
    Rejected,

    /// <summary>Customer asked to return the order; waiting for admin approval.</summary>
    ReturnRequested,

    /// <summary>Admin approved the return; the order is reversed (stock restored).</summary>
    Returned
}

/// <summary>How the customer intends to pay (informational only — settlement is verified manually).</summary>
public enum PaymentMethod
{
    /// <summary>Pay with cash when the order is delivered.</summary>
    CashOnDelivery,

    /// <summary>Pay by bank transfer.</summary>
    Bank
}

/// <summary>Coupon discount type (§5.7).</summary>
public enum DiscountType
{
    Percentage,
    FixedAmount
}

/// <summary>Product condition (§5.7, D6). Nullable on the product.</summary>
public enum ProductCondition
{
    BrandNew,
    Refurbished,
    Old
}
