using System.Globalization;
using FastCart.Application.Common;
using FastCart.Application.Dashboard;
using FastCart.Domain.Enums;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Dashboard;

/// <summary>
/// Admin dashboard reporting (§6.15). Sales/Cost/Profit come from order-line snapshots
/// (<c>UnitPrice</c>/<c>UnitCost</c>) so history is stable across later cost edits (D9).
/// Cancelled, Rejected and Returned orders are excluded from sales/profit aggregates;
/// everything else (awaiting confirmation → delivered) counts as a booked sale.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        from = from.ToUtc();
        to = to.ToUtc();

        var items = CountedOrderItems();
        if (from is not null) items = items.Where(oi => oi.Order.CreatedAt >= from);
        if (to is not null) items = items.Where(oi => oi.Order.CreatedAt <= to);

        var agg = await items
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Sales = g.Sum(oi => oi.LineTotal),
                Cost = g.Sum(oi => oi.UnitCost * oi.Quantity),
                Units = g.Sum(oi => oi.Quantity)
            })
            .FirstOrDefaultAsync(ct);

        var orders = CountedOrders();
        if (from is not null) orders = orders.Where(o => o.CreatedAt >= from);
        if (to is not null) orders = orders.Where(o => o.CreatedAt <= to);
        var orderCount = await orders.CountAsync(ct);

        var sales = Round(agg?.Sales ?? 0m);
        var cost = Round(agg?.Cost ?? 0m);
        var units = agg?.Units ?? 0;
        var aov = orderCount > 0 ? Round(sales / orderCount) : 0m;

        return new DashboardSummaryDto(from, to, sales, cost, sales - cost, orderCount, units, aov);
    }

    public async Task<DashboardRevenueDto> GetRevenueAsync(int? year, CancellationToken ct = default)
    {
        var y = year is > 0 ? year.Value : DateTime.UtcNow.Year;

        var byMonth = await CountedOrders()
            .Where(o => o.CreatedAt.Year == y)
            .GroupBy(o => o.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Revenue = g.Sum(o => o.Total), Count = g.Count() })
            .ToListAsync(ct);

        var lookup = byMonth.ToDictionary(x => x.Month);
        var months = new List<MonthlyRevenueDto>(12);
        for (var m = 1; m <= 12; m++)
        {
            lookup.TryGetValue(m, out var row);
            months.Add(new MonthlyRevenueDto(
                m,
                CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m),
                Round(row?.Revenue ?? 0m),
                row?.Count ?? 0));
        }

        return new DashboardRevenueDto(y, Round(byMonth.Sum(x => x.Revenue)), byMonth.Sum(x => x.Count), months);
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(string? metric, int take, CancellationToken ct = default)
    {
        var n = take is < 1 or > 50 ? 5 : take;
        var byUnits = !string.Equals(metric, "sales", StringComparison.OrdinalIgnoreCase);

        // Order the groupings by their aggregates first, then project — EF can't order by a
        // record property after a constructor projection over a GroupBy.
        var groups = CountedOrderItems().GroupBy(oi => new { oi.ProductId, oi.ProductName });
        groups = byUnits
            ? groups.OrderByDescending(g => g.Sum(oi => oi.Quantity)).ThenByDescending(g => g.Sum(oi => oi.LineTotal))
            : groups.OrderByDescending(g => g.Sum(oi => oi.LineTotal)).ThenByDescending(g => g.Sum(oi => oi.Quantity));

        var rows = await groups
            .Take(n)
            .Select(g => new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(oi => oi.Quantity),
                g.Sum(oi => oi.LineTotal)))
            .ToListAsync(ct);

        return rows.Select(r => r with { Sales = Round(r.Sales) }).ToList();
    }

    public async Task<IReadOnlyList<RecentTransactionDto>> GetRecentTransactionsAsync(int take, CancellationToken ct = default)
    {
        var n = take is < 1 or > 50 ? 10 : take;

        return await _db.Orders.AsNoTracking()
            .OrderByDescending(o => o.Id)
            .Take(n)
            .Select(o => new RecentTransactionDto(
                o.Id, o.OrderNumber, o.CustomerName, o.Total,
                o.PaymentMethod, o.Status, o.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Orders that count toward sales/profit — everything except the reversed terminal states.</summary>
    private IQueryable<Domain.Entities.Order> CountedOrders() =>
        _db.Orders.AsNoTracking().Where(o => !ReversedStatuses.Contains(o.Status));

    private IQueryable<Domain.Entities.OrderItem> CountedOrderItems() =>
        _db.OrderItems.AsNoTracking().Where(oi => !ReversedStatuses.Contains(oi.Order.Status));

    /// <summary>Terminal states whose revenue never counts (cancelled / rejected / returned).</summary>
    private static readonly OrderStatus[] ReversedStatuses =
        { OrderStatus.Cancelled, OrderStatus.Rejected, OrderStatus.Returned };

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
