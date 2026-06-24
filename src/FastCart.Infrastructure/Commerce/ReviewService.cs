using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Reviews;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Commerce;

/// <summary>Product reviews + rating summary (§6.6). Any customer may post (D4).</summary>
public sealed class ReviewService : IReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db) => _db = db;

    public async Task<ProductReviewsResponse> ListAsync(int productId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var page = pageNumber < 1 ? 1 : pageNumber;
        var size = pageSize is < 1 or > 100 ? 20 : pageSize;

        var grouped = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProductId == productId)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = grouped.Sum(g => g.Count);
        var average = total == 0 ? 0 : Math.Round(grouped.Sum(g => g.Rating * g.Count) / (double)total, 2);
        var distribution = Enumerable.Range(1, 5)
            .ToDictionary(star => star, star => grouped.FirstOrDefault(g => g.Rating == star)?.Count ?? 0);

        var items = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(r => new ReviewDto(
                r.Id, r.Rating, r.Comment,
                _db.Users.Where(u => u.Id == r.UserId).Select(u => u.UserName!).FirstOrDefault() ?? "Customer",
                r.CreatedAt))
            .ToListAsync(ct);

        return new ProductReviewsResponse(
            new ReviewSummaryDto(average, total, distribution),
            new PagedResult<ReviewDto>(items, page, size, total));
    }

    public async Task<ReviewDto> CreateAsync(int productId, string userId, CreateReviewRequest request, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var review = new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = request.Rating,
            Comment = request.Comment
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);

        var author = await _db.Users.Where(u => u.Id == userId).Select(u => u.UserName!).FirstOrDefaultAsync(ct) ?? "Customer";
        return new ReviewDto(review.Id, review.Rating, review.Comment, author, review.CreatedAt);
    }

    public async Task DeleteAsync(int reviewId, string userId, bool isAdmin, CancellationToken ct = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException("Review not found.");

        if (!isAdmin && review.UserId != userId)
        {
            throw new ForbiddenException("You can only delete your own review.");
        }

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
    }
}
