using FastCart.Application.Carts;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Wishlists;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Commerce;

/// <summary>Product-level wishlist (§6.8).</summary>
public sealed class WishlistService : IWishlistService
{
    private readonly AppDbContext _db;
    private readonly ICartService _cart;

    public WishlistService(AppDbContext db, ICartService cart)
    {
        _db = db;
        _cart = cart;
    }

    public async Task<IReadOnlyList<WishlistItemDto>> ListAsync(string userId, CancellationToken ct = default) =>
        await _db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WishlistItemDto(
                w.ProductId,
                w.Product.Name,
                w.Product.Images.OrderByDescending(im => im.IsPrimary).ThenBy(im => im.SortOrder).Select(im => im.Url).FirstOrDefault(),
                w.Product.Variants.Where(v => v.IsActive)
                    .Min(v => (decimal?)(v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price)),
                w.Product.Variants.Any(v => v.IsActive && v.StockCount > 0),
                w.Product.Variants.Count(v => v.IsActive)))
            .ToListAsync(ct);

    public async Task AddAsync(string userId, int productId, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var exists = await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct);
        if (!exists)
        {
            _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveAsync(string userId, int productId, CancellationToken ct = default)
    {
        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, ct);
        if (item is not null)
        {
            _db.WishlistItems.Remove(item);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<MoveAllToCartResult> MoveAllToCartAsync(string userId, CancellationToken ct = default)
    {
        var products = await _db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => new
            {
                w.ProductId,
                Variants = w.Product.Variants.Where(v => v.IsActive)
                    .Select(v => new { v.Id, v.StockCount }).ToList()
            })
            .ToListAsync(ct);

        var added = new List<int>();
        var needsSelection = new List<int>();

        foreach (var p in products)
        {
            var inStock = p.Variants.Where(v => v.StockCount > 0).ToList();
            // Exactly one in-stock variant and no ambiguity → add it; otherwise flag for a choice.
            if (p.Variants.Count == 1 && inStock.Count == 1)
            {
                await _cart.AddItemAsync(userId, new AddCartItemRequest { ProductVariantId = inStock[0].Id, Quantity = 1 }, ct);
                var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == p.ProductId, ct);
                if (item is not null) _db.WishlistItems.Remove(item);
                added.Add(p.ProductId);
            }
            else
            {
                needsSelection.Add(p.ProductId);
            }
        }

        await _db.SaveChangesAsync(ct);
        return new MoveAllToCartResult(added, needsSelection);
    }
}
