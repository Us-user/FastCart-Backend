using FastCart.Application.Common;
using FastCart.Application.Orders;
using FastCart.Domain.Common;
using FastCart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Admin order management (§6.11). Base route: <c>/api/v1/admin/orders</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/orders")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly IAdminOrderService _orders;
    public AdminOrdersController(IAdminOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] PaymentStatus? paymentStatus,
        [FromQuery] string? q,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? sort,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new AdminOrderQuery
        {
            Status = status,
            PaymentStatus = paymentStatus,
            Q = q,
            From = from,
            To = to,
            Sort = sort,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        return Ok(ApiResponse<PagedResult<OrderSummaryDto>>.Ok(await _orders.ListAsync(query, ct)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.GetAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateOrderRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.CreateOfflineAsync(request, ct), "Order created."));

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetOrderStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.SetStatusAsync(id, request, ct), "Order status updated."));

    [HttpPut("{id:int}/payment-status")]
    public async Task<IActionResult> SetPaymentStatus(int id, [FromBody] SetPaymentStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.SetPaymentStatusAsync(id, request, ct), "Payment status updated."));
}

/// <summary>Admin return management (§6.11). Base route: <c>/api/v1/admin/returns</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/returns")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminReturnsController : ControllerBase
{
    private readonly IAdminOrderService _orders;
    public AdminReturnsController(IAdminOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ReturnStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<AdminReturnDto>>.Ok(await _orders.ListReturnsAsync(status, pageNumber, pageSize, ct)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveReturnRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AdminReturnDto>.Ok(await _orders.ResolveReturnAsync(id, request, ct), "Return updated."));
}
