using FastCart.Application.Carts;
using FastCart.Application.Common.Exceptions;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Commerce;

/// <summary>Variant-based cart with computed totals (§6.7). Quantities are clamped to stock.</summary>
public sealed class CartService : ICartService
{
    private readonly AppDbContext _db;

    public CartService(AppDbContext db) => _db = db;

    public Task<CartDto> GetAsync(string userId, CancellationToken ct = default) => BuildAsync(userId, ct);

    public async Task<CartDto> AddItemAsync(string userId, AddCartItemRequest request, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, ct)
            ?? throw new NotFoundException("Variant not found.");
        if (!variant.IsActive || variant.StockCount <= 0)
        {
            throw new ConflictException("This item is out of stock.");
        }

        var cart = await GetOrCreateCartAsync(userId, ct);
        var item = await _db.CartItems.FirstOrDefaultAsync(i => i.CartId == cart.Id && i.ProductVariantId == variant.Id, ct);

        var desired = (item?.Quantity ?? 0) + request.Quantity;
        if (desired > variant.StockCount) desired = variant.StockCount;

        if (item is null)
        {
            _db.CartItems.Add(new CartItem { CartId = cart.Id, ProductVariantId = variant.Id, Quantity = desired });
        }
        else
        {
            item.Quantity = desired;
        }

        await _db.SaveChangesAsync(ct);
        return await BuildAsync(userId, ct);
    }

    public async Task<CartDto> SetQuantityAsync(string userId, int variantId, int quantity, CancellationToken ct = default)
    {
        var item = await GetItemAsync(userId, variantId, ct);
        if (quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = Math.Min(quantity, item.ProductVariant.StockCount);
        }

        await _db.SaveChangesAsync(ct);
        return await BuildAsync(userId, ct);
    }

    public async Task<CartDto> IncrementAsync(string userId, int variantId, CancellationToken ct = default)
    {
        var item = await GetItemAsync(userId, variantId, ct);
        if (item.Quantity < item.ProductVariant.StockCount)
        {
            item.Quantity++;
            await _db.SaveChangesAsync(ct);
        }
        return await BuildAsync(userId, ct);
    }

    public async Task<CartDto> DecrementAsync(string userId, int variantId, CancellationToken ct = default)
    {
        var item = await GetItemAsync(userId, variantId, ct);
        item.Quantity--;
        if (item.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        await _db.SaveChangesAsync(ct);
        return await BuildAsync(userId, ct);
    }

    public async Task<CartDto> RemoveItemAsync(string userId, int variantId, CancellationToken ct = default)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(i => i.Cart.UserId == userId && i.ProductVariantId == variantId, ct);
        if (item is not null)
        {
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync(ct);
        }
        return await BuildAsync(userId, ct);
    }

    public async Task<CartDto> ClearAsync(string userId, CancellationToken ct = default)
    {
        var items = await _db.CartItems.Where(i => i.Cart.UserId == userId).ToListAsync(ct);
        if (items.Count > 0)
        {
            _db.CartItems.RemoveRange(items);
            await _db.SaveChangesAsync(ct);
        }
        return await BuildAsync(userId, ct);
    }

    private async Task<Cart> GetOrCreateCartAsync(string userId, CancellationToken ct)
    {
        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
        }
        return cart;
    }

    private async Task<CartItem> GetItemAsync(string userId, int variantId, CancellationToken ct) =>
        await _db.CartItems.Include(i => i.ProductVariant)
            .FirstOrDefaultAsync(i => i.Cart.UserId == userId && i.ProductVariantId == variantId, ct)
        ?? throw new NotFoundException("Cart item not found.");

    private async Task<CartDto> BuildAsync(string userId, CancellationToken ct)
    {
        var raw = await _db.CartItems.AsNoTracking()
            .Where(i => i.Cart.UserId == userId)
            .OrderBy(i => i.Id)
            .Select(i => new
            {
                i.ProductVariantId,
                i.ProductVariant.ProductId,
                ProductName = i.ProductVariant.Product.Name,
                i.ProductVariant.Sku,
                Options = i.ProductVariant.OptionValues
                    .OrderBy(ov => ov.ProductOptionValue.ProductOption.SortOrder)
                    .Select(ov => new { ov.ProductOptionValue.ProductOption.Name, ov.ProductOptionValue.Value }).ToList(),
                ImageUrl = i.ProductVariant.Product.Images
                    .OrderByDescending(im => im.IsPrimary).ThenBy(im => im.SortOrder).Select(im => im.Url).FirstOrDefault(),
                UnitPrice = i.ProductVariant.HasDiscount && i.ProductVariant.DiscountPrice != null
                    ? i.ProductVariant.DiscountPrice!.Value : i.ProductVariant.Price,
                i.Quantity,
                i.ProductVariant.StockCount,
                i.ProductVariant.IsActive
            })
            .ToListAsync(ct);

        var items = raw.Select(r => new CartItemDto(
            r.ProductVariantId, r.ProductId, r.ProductName, r.Sku,
            r.Options.Count > 0 ? string.Join(" / ", r.Options.Select(o => $"{o.Name}: {o.Value}")) : null,
            r.ImageUrl, r.UnitPrice, r.Quantity, r.UnitPrice * r.Quantity, r.StockCount, r.StockCount > 0, r.IsActive))
            .ToList();

        return new CartDto(items, items.Sum(i => i.LineTotal), items.Sum(i => i.Quantity), items.Count);
    }
}
