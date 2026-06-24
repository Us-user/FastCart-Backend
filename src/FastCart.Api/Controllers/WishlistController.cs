using FastCart.Application.Common;
using FastCart.Application.Wishlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Wishlist (§6.8). Base route: <c>/api/v1/wishlist</c>. Product-level.</summary>
[Authorize]
public sealed class WishlistController : BaseApiController
{
    private readonly IWishlistService _wishlist;
    public WishlistController(IWishlistService wishlist) => _wishlist = wishlist;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<WishlistItemDto>>.Ok(await _wishlist.ListAsync(CurrentUserIdRequired, ct)));

    [HttpPost("{productId:int}")]
    public async Task<IActionResult> Add(int productId, CancellationToken ct)
    {
        await _wishlist.AddAsync(CurrentUserIdRequired, productId, ct);
        return Ok(ApiResponse.Ok("Added to wishlist."));
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Remove(int productId, CancellationToken ct)
    {
        await _wishlist.RemoveAsync(CurrentUserIdRequired, productId, ct);
        return Ok(ApiResponse.Ok("Removed from wishlist."));
    }

    [HttpPost("move-all-to-cart")]
    public async Task<IActionResult> MoveAll(CancellationToken ct) =>
        Ok(ApiResponse<MoveAllToCartResult>.Ok(await _wishlist.MoveAllToCartAsync(CurrentUserIdRequired, ct),
            "Eligible items moved to your cart."));
}
