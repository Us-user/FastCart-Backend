using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;

namespace FastCart.Application.Reviews;

/// <summary>Product reviews (§6.6). Any authenticated customer may post (D4).</summary>
public interface IReviewService
{
    Task<ProductReviewsResponse> ListAsync(int productId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<ReviewDto> CreateAsync(int productId, string userId, CreateReviewRequest request, CancellationToken ct = default);
    Task DeleteAsync(int reviewId, string userId, bool isAdmin, CancellationToken ct = default);
}

public sealed record ReviewDto(int Id, int Rating, string? Comment, string AuthorName, DateTime CreatedAt);

/// <summary>Rating summary: average, total, and per-star counts (§6.6).</summary>
public sealed record ReviewSummaryDto(double Average, int Count, IReadOnlyDictionary<int, int> Distribution);

public sealed record ProductReviewsResponse(ReviewSummaryDto Summary, PagedResult<ReviewDto> Reviews);

public sealed record CreateReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; init; }

    [StringLength(2000)]
    public string? Comment { get; init; }
}
