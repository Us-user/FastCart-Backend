using FastCart.Application.Common;
using FastCart.Application.Orders;
using FastCart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Checkout &amp; customer orders (§6.10). Base route: <c>/api/v1/orders</c>.</summary>
[Authorize]
public sealed class OrdersController : BaseApiController
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(
            await _orders.CheckoutAsync(CurrentUserIdRequired, request, ct),
            "Order placed. Payment is pending confirmation."));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<OrderSummaryDto>>.Ok(
            await _orders.ListMineAsync(CurrentUserIdRequired, status, pageNumber, pageSize, ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.GetMineAsync(CurrentUserIdRequired, id, ct)));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelOrderRequest? request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(
            await _orders.CancelAsync(CurrentUserIdRequired, id, request ?? new CancelOrderRequest(), ct),
            "Order cancelled."));

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] CreateReturnRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ReturnRequestDto>.Ok(
            await _orders.RequestReturnAsync(CurrentUserIdRequired, id, request, ct),
            "Return requested."));

    [HttpPost("{id:int}/pay")]
    public async Task<IActionResult> Pay(int id, [FromBody] PayOrderRequest? request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(
            await _orders.PayAsync(CurrentUserIdRequired, id, request ?? new PayOrderRequest(), ct),
            "Payment recorded."));
}

/// <summary>Customer return requests (§6.10, "My Returns"). Base route: <c>/api/v1/returns</c>.</summary>
[Authorize]
public sealed class ReturnsController : BaseApiController
{
    private readonly IOrderService _orders;
    public ReturnsController(IOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ReturnRequestDto>>.Ok(await _orders.ListMyReturnsAsync(CurrentUserIdRequired, ct)));
}
