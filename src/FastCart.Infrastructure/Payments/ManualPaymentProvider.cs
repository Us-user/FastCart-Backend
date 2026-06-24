using FastCart.Application.Payments;
using FastCart.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace FastCart.Infrastructure.Payments;

/// <summary>
/// Manual/Test provider for the Bank method (§7.3, D2). The buyer may submit any payment
/// data; nothing sensitive is persisted (D3). By default it records the payment as
/// <see cref="PaymentStatus.Pending"/> ("record and hold"). Two config toggles under
/// <c>Payments:Bank</c> let tests exercise both branches without a real gateway:
/// <list type="bullet">
///   <item><c>SimulateFailure</c> — returns <see cref="PaymentStatus.Failed"/>.</item>
///   <item><c>AutoMarkPaid</c> — returns <see cref="PaymentStatus.Paid"/> immediately.</item>
/// </list>
/// </summary>
public sealed class ManualPaymentProvider : IPaymentProvider
{
    private readonly IConfiguration _config;

    public ManualPaymentProvider(IConfiguration config) => _config = config;

    public string Name => "Manual";

    public bool CanHandle(PaymentMethod method) => method == PaymentMethod.Bank;

    public Task<PaymentResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        var section = _config.GetSection("Payments:Bank");

        if (section.GetValue<bool>("SimulateFailure"))
        {
            return Task.FromResult(new PaymentResult(Name, PaymentStatus.Failed, request.Reference, PaidAt: null));
        }

        if (section.GetValue<bool>("AutoMarkPaid"))
        {
            return Task.FromResult(new PaymentResult(Name, PaymentStatus.Paid, request.Reference, DateTime.UtcNow));
        }

        return Task.FromResult(new PaymentResult(Name, PaymentStatus.Pending, request.Reference, PaidAt: null));
    }
}
