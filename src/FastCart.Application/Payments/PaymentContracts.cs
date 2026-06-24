using FastCart.Domain.Enums;

namespace FastCart.Application.Payments;

/// <summary>
/// Non-sensitive charge request handed to a payment provider (§7.3). No card PAN or CVV
/// is ever included — at most a non-sensitive reference/note (D3).
/// </summary>
public sealed record PaymentChargeRequest(
    string OrderNumber,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string? Reference);

/// <summary>Outcome of recording/processing a payment (§7.3).</summary>
public sealed record PaymentResult(
    string Provider,
    PaymentStatus Status,
    string? Reference,
    DateTime? PaidAt);

/// <summary>
/// Payment abstraction (D2/§7.3): every method records behind one interface so a real
/// gateway is a later swap with no change to order logic. The current providers are
/// "record and hold" — they create the payment as <see cref="PaymentStatus.Pending"/>,
/// and an admin (or a config toggle for the test provider) flips it later.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Stored on the <c>Payment.Provider</c> column (e.g. "Manual" / "CashOnDelivery").</summary>
    string Name { get; }

    bool CanHandle(PaymentMethod method);

    Task<PaymentResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default);
}

/// <summary>Selects the registered provider that handles a given payment method (§7.3).</summary>
public interface IPaymentProviderResolver
{
    IPaymentProvider Resolve(PaymentMethod method);
}
