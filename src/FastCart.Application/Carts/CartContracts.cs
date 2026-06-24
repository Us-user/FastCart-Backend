using System.ComponentModel.DataAnnotations;

namespace FastCart.Application.Carts;

/// <summary>Cart operations on variants (§6.7). All scoped to the calling user.</summary>
public interface ICartService
{
    Task<CartDto> GetAsync(string userId, CancellationToken ct = default);
    Task<CartDto> AddItemAsync(string userId, AddCartItemRequest request, CancellationToken ct = default);
    Task<CartDto> SetQuantityAsync(string userId, int variantId, int quantity, CancellationToken ct = default);
    Task<CartDto> IncrementAsync(string userId, int variantId, CancellationToken ct = default);
    Task<CartDto> DecrementAsync(string userId, int variantId, CancellationToken ct = default);
    Task<CartDto> RemoveItemAsync(string userId, int variantId, CancellationToken ct = default);
    Task<CartDto> ClearAsync(string userId, CancellationToken ct = default);
}

public sealed record CartDto(
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal,
    int TotalQuantity,
    int ItemCount);

public sealed record CartItemDto(
    int ProductVariantId,
    int ProductId,
    string ProductName,
    string Sku,
    string? VariantDescription,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int StockCount,
    bool InStock,
    bool IsActive);

public sealed record AddCartItemRequest
{
    [Required]
    public int ProductVariantId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; } = 1;
}

public sealed record SetQuantityRequest
{
    [Range(0, int.MaxValue)]
    public int Quantity { get; init; }
}
