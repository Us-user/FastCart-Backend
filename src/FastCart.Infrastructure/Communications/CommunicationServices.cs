using FastCart.Application.Common;
using FastCart.Application.Communications;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Communications;

internal static class Paging
{
    public static (int page, int size) Normalize(int pageNumber, int pageSize) =>
        (pageNumber < 1 ? 1 : pageNumber, pageSize is < 1 or > 100 ? 20 : pageSize);
}

/// <summary>Newsletter subscribe + admin listing (§6.13).</summary>
public sealed class NewsletterService : INewsletterService
{
    private readonly AppDbContext _db;

    public NewsletterService(AppDbContext db) => _db = db;

    public async Task SubscribeAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (await _db.NewsletterSubscribers.AnyAsync(s => s.Email == normalized, ct))
        {
            return; // idempotent — already subscribed
        }

        _db.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = normalized });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NewsletterSubscriberDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var (page, size) = Paging.Normalize(pageNumber, pageSize);
        var query = _db.NewsletterSubscribers.AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(s => new NewsletterSubscriberDto(s.Id, s.Email, s.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<NewsletterSubscriberDto>(items, page, size, total);
    }
}

/// <summary>Contact-form submission + admin listing (§6.13).</summary>
public sealed class ContactService : IContactService
{
    private readonly AppDbContext _db;

    public ContactService(AppDbContext db) => _db = db;

    public async Task CreateAsync(ContactMessageRequest request, CancellationToken ct = default)
    {
        _db.ContactMessages.Add(new ContactMessage
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Message = request.Message.Trim()
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<ContactMessageDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var (page, size) = Paging.Normalize(pageNumber, pageSize);
        var query = _db.ContactMessages.AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(m => new ContactMessageDto(m.Id, m.Name, m.Email, m.Phone, m.Message, m.IsRead, m.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ContactMessageDto>(items, page, size, total);
    }
}
