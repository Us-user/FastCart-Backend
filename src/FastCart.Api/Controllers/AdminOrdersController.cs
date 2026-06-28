using FastCart.Application.Common;
using FastCart.Application.Orders;
using FastCart.Domain.Common;
using FastCart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>
/// Admin order management (§6.11). Base route: <c>/api/v1/admin/orders</c>.
/// The lifecycle is driven by explicit actions: confirm (→ InTransit), reject (→ Rejected),
/// deliver (→ Delivered), and for returns approve (→ Returned) / decline (roll back).
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/orders")]
[Authorize(Roles = Roles.Management)]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly IAdminOrderService _orders;
    public AdminOrdersController(IAdminOrderService orders) => _orders = orders;

    /// <summary>
    /// List orders with optional filters. Use <c>?status=AwaitingConfirmation</c> for the
    /// "to confirm" queue and <c>?status=ReturnRequested</c> for pending returns.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OrderSummaryDto>>), 200)]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
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
            Q = q,
            From = from,
            To = to,
            Sort = sort,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        return Ok(ApiResponse<PagedResult<OrderSummaryDto>>.Ok(await _orders.ListAsync(query, ct)));
    }

    /// <summary>Get one order with full detail and its lifecycle timestamps.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.GetAsync(id, ct)));

    /// <summary>Create a manual/offline order (walk-in, phone). Starts as <c>AwaitingConfirmation</c>.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Create([FromBody] AdminCreateOrderRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.CreateOfflineAsync(request, ct), "Order created."));

    /// <summary>Confirm an order awaiting confirmation. <c>AwaitingConfirmation</c> → <c>InTransit</c>.</summary>
    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.ConfirmAsync(id, ct), "Order confirmed — now in transit."));

    /// <summary>Reject an order awaiting confirmation, e.g. price/stock issue. Restores stock. <c>AwaitingConfirmation</c> → <c>Rejected</c>.</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectOrderRequest? request, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.RejectAsync(id, request ?? new RejectOrderRequest(), ct), "Order rejected."));

    /// <summary>Mark an in-transit order as delivered. <c>InTransit</c> → <c>Delivered</c>.</summary>
    [HttpPost("{id:int}/deliver")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> Deliver(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.MarkDeliveredAsync(id, ct), "Order marked as delivered."));

    /// <summary>Approve a requested return. Restores stock. <c>ReturnRequested</c> → <c>Returned</c>.</summary>
    [HttpPost("{id:int}/return/approve")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> ApproveReturn(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.ApproveReturnAsync(id, ct), "Return approved."));

    /// <summary>Decline a requested return; the order rolls back to its previous status.</summary>
    [HttpPost("{id:int}/return/decline")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), 200)]
    public async Task<IActionResult> DeclineReturn(int id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await _orders.DeclineReturnAsync(id, ct), "Return declined."));
}
