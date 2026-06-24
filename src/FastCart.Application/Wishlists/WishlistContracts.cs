namespace FastCart.Application.Wishlists;

/// <summary>Product-level wishlist (§6.8).</summary>
public interface IWishlistService
{
    Task<IReadOnlyList<WishlistItemDto>> ListAsync(string userId, CancellationToken ct = default);
    Task AddAsync(string userId, int productId, CancellationToken ct = default);
    Task RemoveAsync(string userId, int productId, CancellationToken ct = default);
    Task<MoveAllToCartResult> MoveAllToCartAsync(string userId, CancellationToken ct = default);
}

public sealed record WishlistItemDto(
    int ProductId,
    string Name,
    string? PrimaryImageUrl,
    decimal? FromPrice,
    bool InStock,
    int ActiveVariantCount);

/// <summary>
/// "Move All To Bag" outcome (§6.8): products with one in-stock variant are added;
/// those needing an option choice (or out of stock) are flagged for the UI.
/// </summary>
public sealed record MoveAllToCartResult(
    IReadOnlyList<int> AddedProductIds,
    IReadOnlyList<int> NeedsSelectionProductIds);
