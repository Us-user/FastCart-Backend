using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Orders;
using FastCart.Domain.Entities;
using FastCart.Domain.Enums;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Orders;

/// <summary>
/// Admin order management (§6.11). Moves orders through their lifecycle via explicit actions
/// (confirm / reject / deliver / approve-return / decline-return) and restores stock whenever an
/// order is rejected or returned (§7.4).
/// </summary>
public sealed class AdminOrderService : IAdminOrderService
{
    private readonly AppDbContext _db;

    public AdminOrderService(AppDbContext db) => _db = db;

    public async Task<PagedResult<OrderSummaryDto>> ListAsync(AdminOrderQuery query, CancellationToken ct = default)
    {
        var page = query.PageNumber < 1 ? 1 : query.PageNumber;
        var size = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var q = _db.Orders.AsNoTracking().AsQueryable();
        if (query.Status is not null) q = q.Where(o => o.Status == query.Status);
        var from = query.From.ToUtc();
        var to = query.To.ToUtc();
        if (from is not null) q = q.Where(o => o.CreatedAt >= from);
        if (to is not null) q = q.Where(o => o.CreatedAt <= to);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{term}%")
                          || EF.Functions.ILike(o.CustomerName, $"%{term}%")
                          || EF.Functions.ILike(o.CustomerEmail, $"%{term}%"));
        }

        q = query.Sort switch
        {
            "oldest" => q.OrderBy(o => o.Id),
            "total_desc" => q.OrderByDescending(o => o.Total).ThenByDescending(o => o.Id),
            "total_asc" => q.OrderBy(o => o.Total).ThenByDescending(o => o.Id),
            _ => q.OrderByDescending(o => o.Id) // newest
        };

        var total = await q.CountAsync(ct);
        var rows = await q
            .Skip((page - 1) * size).Take(size)
            .Select(o => new OrderSummaryDto(
                o.Id, o.OrderNumber, o.Status, o.PaymentMethod, o.Total, o.Items.Count, o.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryDto>(rows, page, size, total);
    }

    public async Task<OrderDto> GetAsync(int id, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Order not found.");
        return OrderingHelpers.MapOrder(order);
    }

    public async Task<OrderDto> CreateOfflineAsync(AdminCreateOrderRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var variantIds = request.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.OptionValues).ThenInclude(ov => ov.ProductOptionValue).ThenInclude(pv => pv.ProductOption)
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(ct);

        decimal subtotal = 0m;
        var orderItems = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            var v = variants.FirstOrDefault(x => x.Id == line.ProductVariantId)
                ?? throw new NotFoundException($"Variant {line.ProductVariantId} not found.");
            if (!v.IsActive)
            {
                throw new ConflictException($"'{v.Product.Name}' ({v.Sku}) is not available.");
            }
            if (v.StockCount < line.Quantity)
            {
                throw new ConflictException($"Only {v.StockCount} left of '{v.Product.Name}' ({v.Sku}).");
            }

            var unitPrice = v.Product.EffectivePrice;
            var lineTotal = Math.Round(unitPrice * line.Quantity, 2);
            subtotal += lineTotal;
            v.StockCount -= line.Quantity;

            orderItems.Add(new OrderItem
            {
                ProductId = v.ProductId,
                ProductVariantId = v.Id,
                ProductName = v.Product.Name,
                Sku = v.Sku,
                VariantDescription = OrderingHelpers.DescribeVariant(v),
                UnitPrice = unitPrice,
                UnitCost = v.Product.CostPrice,
                Quantity = line.Quantity,
                LineTotal = lineTotal
            });
        }
        subtotal = Math.Round(subtotal, 2);

        var ship = request.ShippingAddress;
        var order = new Order
        {
            OrderNumber = await OrderingHelpers.GenerateOrderNumberAsync(_db, ct),
            UserId = null, // offline order (§5.5).
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Status = OrderStatus.AwaitingConfirmation,
            PaymentMethod = request.PaymentMethod,
            Currency = "USD",
            Subtotal = subtotal,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            ShippingAmount = 0m,
            Total = subtotal,
            CustomerNote = request.CustomerNote,
            ShipFirstName = ship.FirstName,
            ShipLastName = ship.LastName,
            ShipStreetAddress = ship.StreetAddress,
            ShipApartment = ship.Apartment,
            ShipCity = ship.City,
            ShipPhone = ship.PhoneNumber,
            ShipEmail = ship.Email,
            Items = orderItems
        };

        if (request.BillingAddress is { } bill)
        {
            order.BillFirstName = bill.FirstName;
            order.BillLastName = bill.LastName;
            order.BillStreetAddress = bill.StreetAddress;
            order.BillApartment = bill.Apartment;
            order.BillCity = bill.City;
            order.BillPhone = bill.PhoneNumber;
            order.BillEmail = bill.Email;
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetAsync(order.Id, ct);
    }

    public async Task<OrderDto> ConfirmAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        Require(order.Status == OrderStatus.AwaitingConfirmation,
            "Only an order awaiting confirmation can be confirmed.");

        order.Status = OrderStatus.InTransit;
        order.ConfirmedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDto> RejectAsync(int id, RejectOrderRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = await LoadWithItemsAsync(id, ct);
        Require(order.Status == OrderStatus.AwaitingConfirmation,
            "Only an order awaiting confirmation can be rejected.");

        order.Status = OrderStatus.Rejected;
        order.RejectedAt = DateTime.UtcNow;
        order.RejectReason = request.Reason;
        await OrderingHelpers.RestoreStockAsync(_db, order, ct); // §7.4

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDto> MarkDeliveredAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        Require(order.Status == OrderStatus.InTransit,
            "Only an order that is in transit can be marked delivered.");

        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDto> ApproveReturnAsync(int id, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = await LoadWithItemsAsync(id, ct);
        Require(order.Status == OrderStatus.ReturnRequested,
            "No return has been requested for this order.");

        order.Status = OrderStatus.Returned;
        order.ReturnedAt = DateTime.UtcNow;
        order.StatusBeforeReturn = null;
        await OrderingHelpers.RestoreStockAsync(_db, order, ct); // §7.4

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDto> DeclineReturnAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        Require(order.Status == OrderStatus.ReturnRequested,
            "No return has been requested for this order.");

        // Roll back to wherever the order was (in transit / delivered) before the return.
        order.Status = order.StatusBeforeReturn ?? OrderStatus.Delivered;
        order.StatusBeforeReturn = null;
        order.ReturnRequestedAt = null;
        order.ReturnReason = null;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---- helpers --------------------------------------------------------------

    private async Task<Order> LoadAsync(int id, CancellationToken ct) =>
        await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException("Order not found.");

    private async Task<Order> LoadWithItemsAsync(int id, CancellationToken ct) =>
        await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException("Order not found.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new BusinessRuleException(message);
    }
}
