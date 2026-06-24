using FastCart.Application.Payments;
using FastCart.Domain.Enums;

namespace FastCart.Infrastructure.Payments;

/// <summary>
/// Cash on delivery (§7.3): real, no integration. The order is created and the payment
/// recorded as <see cref="PaymentStatus.Pending"/>; an admin marks it Paid after delivery.
/// </summary>
public sealed class CashOnDeliveryPaymentProvider : IPaymentProvider
{
    public string Name => "CashOnDelivery";

    public bool CanHandle(PaymentMethod method) => method == PaymentMethod.CashOnDelivery;

    public Task<PaymentResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PaymentResult(Name, PaymentStatus.Pending, request.Reference, PaidAt: null));
}
