using FastCart.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace FastCart.Infrastructure.Messaging;

/// <summary>
/// Development email sender — logs the message (including password-reset tokens) instead
/// of sending (§4.4). A real SMTP/provider sender swaps in behind <see cref="IEmailSender"/>.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("DEV EMAIL → {To} | {Subject}{NewLine}{Body}", to, subject, Environment.NewLine, body);
        return Task.CompletedTask;
    }
}
