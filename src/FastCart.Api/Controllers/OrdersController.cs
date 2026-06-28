using FastCart.Application.Common;
using FastCart.Application.Orders;
using FastCart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>
/// Checkout &amp; customer orders (§6.10). Base route: <c>/api/v1/orders</c>.
/// Lifecycle: <c>AwaitingConfirmation</c> → <c>InTransit</c> → <c>Delivered</c>; the customer
/// may cancel while awaiting confirmation, or request a return once in transit/delivered.
/// </summary>
[Authorize]
public sealed class OrdersController : BaseApiController
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>Place an order from the current cart. Reserves stock and starts as <c>AwaitingConfirmation</c>.</summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(
            await _orders.CheckoutAsync(CurrentUserIdRequired, request, ct),
            "Order placed. Waiting for the store to confirm it."));

    /// <summary>List my orders, optionally filtered by status (e.g. <c>?status=InTransit</c>).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OrderSummaryDto>>), 200)]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<OrderSummaryDto>>.Ok(
            await _orders.ListMineAsync(CurrentUserIdRequired, status, pageNumber, pageSize, ct)));

    /// <summary>Get one of my orders with its full detail and current status.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.GetMineAsync(CurrentUserIdRequired, id, ct)));

    /// <summary>Cancel my order while it is still <c>AwaitingConfirmation</c>. Stock is restored.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelOrderRequest? request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(
            await _orders.CancelAsync(CurrentUserIdRequired, id, request ?? new CancelOrderRequest(), ct),
            "Order cancelled."));

    /// <summary>Request a return for an <c>InTransit</c> or <c>Delivered</c> order. Awaits admin approval.</summary>
    [HttpPost("{id:int}/return")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Return(int id, [FromBody] CreateReturnRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(
            await _orders.RequestReturnAsync(CurrentUserIdRequired, id, request, ct),
            "Return requested. Waiting for the store to approve it."));
}
