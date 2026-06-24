using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;

namespace FastCart.Application.Communications;

// ── Newsletter (§6.13) ───────────────────────────────────────────────────────

public interface INewsletterService
{
    /// <summary>Subscribe an email. Idempotent — re-subscribing succeeds silently.</summary>
    Task SubscribeAsync(string email, CancellationToken ct = default);

    Task<PagedResult<NewsletterSubscriberDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct = default);
}

public sealed record NewsletterSubscriberDto(int Id, string Email, DateTime SubscribedAt);

public sealed record NewsletterSubscribeRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = default!;
}

// ── Contact messages (§6.13) ─────────────────────────────────────────────────

public interface IContactService
{
    Task CreateAsync(ContactMessageRequest request, CancellationToken ct = default);
    Task<PagedResult<ContactMessageDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct = default);
}

public sealed record ContactMessageDto(int Id, string Name, string Email, string? Phone, string Message, bool IsRead, DateTime CreatedAt);

public sealed record ContactMessageRequest
{
    [Required, StringLength(150)]
    public string Name { get; init; } = default!;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = default!;

    [Phone, StringLength(40)]
    public string? Phone { get; init; }

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Message { get; init; } = default!;
}
