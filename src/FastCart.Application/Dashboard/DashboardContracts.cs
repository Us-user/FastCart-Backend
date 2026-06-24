using FastCart.Domain.Enums;

namespace FastCart.Application.Dashboard;

/// <summary>
/// Admin dashboard analytics (§6.15). All figures derive from order-line snapshots
/// (<c>UnitPrice</c>/<c>UnitCost</c>) so profit stays stable even if catalog costs
/// change later (D9). Cancelled and Returned orders are excluded from sales/profit.
/// </summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<DashboardRevenueDto> GetRevenueAsync(int? year, CancellationToken ct = default);
    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(string? metric, int take, CancellationToken ct = default);
    Task<IReadOnlyList<RecentTransactionDto>> GetRecentTransactionsAsync(int take, CancellationToken ct = default);
}

/// <summary>Sales / Cost / Profit over an optional date range (§6.15).</summary>
public sealed record DashboardSummaryDto(
    DateTime? From,
    DateTime? To,
    decimal Sales,
    decimal Cost,
    decimal Profit,
    int OrderCount,
    int UnitsSold,
    decimal AverageOrderValue);

/// <summary>Revenue (order totals) and order counts per calendar month (§6.15).</summary>
public sealed record MonthlyRevenueDto(
    int Month,
    string MonthName,
    decimal Revenue,
    int OrderCount);

public sealed record DashboardRevenueDto(
    int Year,
    decimal Total,
    int OrderCount,
    IReadOnlyList<MonthlyRevenueDto> Months);

/// <summary>Top product by sales value or units sold (§6.15).</summary>
public sealed record TopProductDto(
    int? ProductId,
    string ProductName,
    int UnitsSold,
    decimal Sales);

/// <summary>Recent order/payment activity for the dashboard feed (§6.15).</summary>
public sealed record RecentTransactionDto(
    int OrderId,
    string OrderNumber,
    string CustomerName,
    decimal Total,
    PaymentStatus PaymentStatus,
    PaymentMethod PaymentMethod,
    OrderStatus Status,
    DateTime CreatedAt);
