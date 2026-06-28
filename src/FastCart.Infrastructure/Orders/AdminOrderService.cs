using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Orders;
using FastCart.Domain.Entities;
using FastCart.Domain.Enums;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Orders;

/// <summary>
/// Admin order &amp; return management (§6.11). Enforces the fulfillment lifecycle (§7.2)
/// and restores stock / refunds on cancellation or completed return (§7.4).
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
        if (query.PaymentStatus is not null) q = q.Where(o => o.PaymentStatus == query.PaymentStatus);
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
                o.Id, o.OrderNumber, o.Status, o.PaymentStatus, o.PaymentMethod, o.Total, o.Items.Count, o.CreatedAt))
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
        var paymentStatus = request.PaymentStatus ?? PaymentStatus.Pending;
        var order = new Order
        {
            OrderNumber = await OrderingHelpers.GenerateOrderNumberAsync(_db, ct),
            UserId = null, // offline order (§5.5).
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Status = OrderStatus.New,
            PaymentStatus = paymentStatus,
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

        order.Payments.Add(new Payment
        {
            Method = request.PaymentMethod,
            Provider = request.PaymentMethod == PaymentMethod.CashOnDelivery ? "CashOnDelivery" : "Manual",
            Amount = subtotal,
            Currency = order.Currency,
            Status = paymentStatus,
            PaidAt = paymentStatus == PaymentStatus.Paid ? DateTime.UtcNow : null
        });

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetAsync(order.Id, ct);
    }

    public async Task<OrderDto> SetStatusAsync(int id, SetOrderStatusRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Order not found.");

        if (request.Status == order.Status)
        {
            throw new BusinessRuleException($"Order is already {order.Status}.");
        }
        if (!IsValidTransition(order.Status, request.Status))
        {
            throw new BusinessRuleException($"Cannot move an order from {order.Status} to {request.Status}.");
        }

        // Restore stock when moving into a stock-returning terminal state (§7.4).
        if (request.Status is OrderStatus.Cancelled or OrderStatus.Returned)
        {
            await OrderingHelpers.RestoreStockAsync(_db, order, ct);
        }
        if (request.Status == OrderStatus.Cancelled)
        {
            order.CancelledAt = DateTime.UtcNow;
            order.CancelReason = request.Reason;
        }

        order.Status = request.Status;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<OrderDto> SetPaymentStatusAsync(int id, SetPaymentStatusRequest request, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Payments).Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Order not found.");

        order.PaymentStatus = request.PaymentStatus;

        // Reflect on the most recent payment record (§7.3).
        var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
        if (payment is not null)
        {
            payment.Status = request.PaymentStatus;
            payment.PaidAt = request.PaymentStatus == PaymentStatus.Paid ? (payment.PaidAt ?? DateTime.UtcNow) : null;
        }

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PagedResult<AdminReturnDto>> ListReturnsAsync(ReturnStatus? status, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var page = pageNumber < 1 ? 1 : pageNumber;
        var size = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = _db.ReturnRequests.AsNoTracking().AsQueryable();
        if (status is not null) q = q.Where(r => r.Status == status);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(r => r.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(r => new AdminReturnDto(
                r.Id, r.OrderId, r.Order.OrderNumber, r.Order.UserId, r.Order.CustomerName,
                r.Reason, r.Status, r.CreatedAt, r.ResolvedAt))
            .ToListAsync(ct);

        return new PagedResult<AdminReturnDto>(rows, page, size, total);
    }

    public async Task<AdminReturnDto> ResolveReturnAsync(int id, ResolveReturnRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var rr = await _db.ReturnRequests.Include(r => r.Order).ThenInclude(o => o.Items)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Return request not found.");

        switch (request.Status)
        {
            case ReturnStatus.Approved:
            case ReturnStatus.Rejected:
                if (rr.Status != ReturnStatus.Requested)
                {
                    throw new BusinessRuleException($"Only a requested return can be {request.Status}.");
                }
                rr.Status = request.Status;
                if (request.Status == ReturnStatus.Rejected) rr.ResolvedAt = DateTime.UtcNow;
                break;

            case ReturnStatus.Completed:
                if (rr.Status != ReturnStatus.Approved)
                {
                    throw new BusinessRuleException("Only an approved return can be completed.");
                }
                rr.Status = ReturnStatus.Completed;
                rr.ResolvedAt = DateTime.UtcNow;

                // §7.4 — completing a return restores stock, marks the order Returned and refunds.
                await OrderingHelpers.RestoreStockAsync(_db, rr.Order, ct);
                rr.Order.Status = OrderStatus.Returned;
                rr.Order.PaymentStatus = PaymentStatus.Refunded;
                break;

            default:
                throw new BusinessRuleException("Resolve a return as Approved, Rejected or Completed.");
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new AdminReturnDto(
            rr.Id, rr.OrderId, rr.Order.OrderNumber, rr.Order.UserId, rr.Order.CustomerName,
            rr.Reason, rr.Status, rr.CreatedAt, rr.ResolvedAt);
    }

    /// <summary>Allowed fulfillment transitions (§7.2).</summary>
    private static bool IsValidTransition(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.New => to is OrderStatus.Ready or OrderStatus.Cancelled,
        OrderStatus.Ready => to is OrderStatus.Shipped or OrderStatus.Cancelled,
        OrderStatus.Shipped => to is OrderStatus.Received,
        OrderStatus.Received => to is OrderStatus.Returned,
        _ => false // Cancelled / Returned are terminal.
    };
}
