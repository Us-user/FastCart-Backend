using FastCart.Application.Common.Exceptions;
using FastCart.Application.Payments;
using FastCart.Domain.Enums;

namespace FastCart.Infrastructure.Payments;

/// <summary>Picks the registered <see cref="IPaymentProvider"/> for a payment method (§7.3).</summary>
public sealed class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly IEnumerable<IPaymentProvider> _providers;

    public PaymentProviderResolver(IEnumerable<IPaymentProvider> providers) => _providers = providers;

    public IPaymentProvider Resolve(PaymentMethod method) =>
        _providers.FirstOrDefault(p => p.CanHandle(method))
        ?? throw new BusinessRuleException($"No payment provider is configured for method '{method}'.");
}
