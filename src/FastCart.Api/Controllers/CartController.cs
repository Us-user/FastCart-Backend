using FastCart.Application.Carts;
using FastCart.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Cart (§6.7). Base route: <c>/api/v1/cart</c>. Operates on variants.</summary>
[Authorize]
public sealed class CartController : BaseApiController
{
    private readonly ICartService _cart;
    public CartController(ICartService cart) => _cart = cart;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse<CartDto>.Ok(await _cart.GetAsync(CurrentUserIdRequired, ct)));

    [HttpPost("items")]
    public async Task<IActionResult> Add([FromBody] AddCartItemRequest request, CancellationToken ct) =>
        Ok(ApiResponse<CartDto>.Ok(await _cart.AddItemAsync(CurrentUserIdRequired, request, ct), "Item added to cart."));

    [HttpPut("items/{variantId:int}")]
    public async Task<IActionResult> SetQuantity(int variantId, [FromBody] SetQuantityRequest request, CancellationToken ct) =>
        Ok(ApiResponse<CartDto>.Ok(await _cart.SetQuantityAsync(CurrentUserIdRequired, variantId, request.Quantity, ct), "Cart updated."));

    [HttpPost("items/{variantId:int}/increment")]
    public async Task<IActionResult> Increment(int variantId, CancellationToken ct) =>
        Ok(ApiResponse<CartDto>.Ok(await _cart.IncrementAsync(CurrentUserIdRequired, variantId, ct)));

    [HttpPost("items/{variantId:int}/decrement")]
    public async Task<IActionResult> Decrement(int variantId, CancellationToken ct) =>
        Ok(ApiResponse<CartDto>.Ok(await _cart.DecrementAsync(CurrentUserIdRequired, variantId, ct)));

    [HttpDelete("items/{variantId:int}")]
    public async Task<IActionResult> Remove(int variantId, CancellationToken ct) =>
        Ok(ApiResponse<CartDto>.Ok(await _cart.RemoveItemAsync(CurrentUserIdRequired, variantId, ct), "Item removed."));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct) =>
        Ok(ApiResponse<CartDto>.Ok(await _cart.ClearAsync(CurrentUserIdRequired, ct), "Cart cleared."));
}
