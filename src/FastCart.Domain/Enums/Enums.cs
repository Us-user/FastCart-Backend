namespace FastCart.Domain.Enums;

/// <summary>Order fulfillment status (§5.7, §7.2).</summary>
public enum OrderStatus
{
    New,
    Ready,
    Shipped,
    Received,
    Cancelled,
    Returned
}

/// <summary>Payment status, independent of fulfillment (§5.7, §7.2).</summary>
public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded
}

/// <summary>Payment method (§5.7).</summary>
public enum PaymentMethod
{
    CashOnDelivery,
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

/// <summary>Return-request status (§5.7).</summary>
public enum ReturnStatus
{
    Requested,
    Approved,
    Rejected,
    Completed
}
