using FastCart.Application.Common;
using FastCart.Application.Dashboard;
using FastCart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Admin dashboard &amp; reporting (§6.15). Base route: <c>/api/v1/admin/dashboard</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/dashboard")]
[Authorize(Roles = Roles.Management)]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    public AdminDashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Sales, Cost and Profit over an optional date range (defaults to all-time).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct) =>
        Ok(ApiResponse<DashboardSummaryDto>.Ok(await _dashboard.GetSummaryAsync(from, to, ct)));

    /// <summary>Monthly revenue + order counts for a year (defaults to the current year).</summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] int? year, CancellationToken ct) =>
        Ok(ApiResponse<DashboardRevenueDto>.Ok(await _dashboard.GetRevenueAsync(year, ct)));

    /// <summary>Top products by <c>sales</c> value or <c>units</c> sold (default: units, take 5).</summary>
    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts(
        [FromQuery] string? metric,
        [FromQuery] int take = 5,
        CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<TopProductDto>>.Ok(await _dashboard.GetTopProductsAsync(metric, take, ct)));

    /// <summary>Most recent orders/transactions (default: 10).</summary>
    [HttpGet("recent-transactions")]
    public async Task<IActionResult> RecentTransactions(
        [FromQuery] int take = 10,
        CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<RecentTransactionDto>>.Ok(await _dashboard.GetRecentTransactionsAsync(take, ct)));
}
