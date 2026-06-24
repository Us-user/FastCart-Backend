namespace FastCart.Application.Common.Interfaces;

/// <summary>
/// Outbound email abstraction (§4.4). The Phase 2 implementation logs messages
/// (dev); a real SMTP/provider sender is a later swap behind this interface.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
