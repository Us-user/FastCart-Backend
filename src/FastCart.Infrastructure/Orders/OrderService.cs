using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Orders;
using FastCart.Domain.Entities;
using FastCart.Domain.Enums;
using FastCart.Infrastructure.Commerce;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FastCart.Infrastructure.Orders;

/// <summary>
/// Checkout and customer order lifecycle (§6.10, §7.1–7.5). Checkout and cancel run in a single
/// transaction; prices, the coupon discount and stock are recomputed server-side and never trusted
/// from the client. Stock is reserved at checkout and restored on cancel/reject/return (§7.4).
/// Payment is not tracked here — the customer only picks a method; an admin verifies settlement.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public OrderService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<OrderDto> CheckoutAsync(string userId, CheckoutRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product)
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.OptionValues)
                .ThenInclude(ov => ov.ProductOptionValue).ThenInclude(pv => pv.ProductOption)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is null || cart.Items.Count == 0)
        {
            throw new BusinessRuleException("Your cart is empty.");
        }

        var ship = await ResolveShippingAsync(userId, request, ct);

        // 2–3. Validate each line against live stock and recompute prices server-side (§7.1).
        decimal subtotal = 0m;
        decimal taxableSubtotal = 0m;
        var orderItems = new List<OrderItem>();
        foreach (var item in cart.Items.OrderBy(i => i.Id))
        {
            var v = item.ProductVariant;
            if (!v.IsActive)
            {
                throw new ConflictException($"'{v.Product.Name}' ({v.Sku}) is no longer available.");
            }
            if (v.StockCount < item.Quantity)
            {
                throw new ConflictException($"Only {v.StockCount} left of '{v.Product.Name}' ({v.Sku}).");
            }

            var unitPrice = v.Product.EffectivePrice;
            var lineTotal = Math.Round(unitPrice * item.Quantity, 2);
            subtotal += lineTotal;
            if (v.Product.IsTaxable) taxableSubtotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                ProductId = v.ProductId,
                ProductVariantId = v.Id,
                ProductName = v.Product.Name,
                Sku = v.Sku,
                VariantDescription = OrderingHelpers.DescribeVariant(v),
                UnitPrice = unitPrice,
                UnitCost = v.Product.CostPrice,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });
        }
        subtotal = Math.Round(subtotal, 2);

        // 4. Coupon — recomputed authoritatively (§7.5); never taken from the client.
        decimal discount = 0m;
        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == request.CouponCode, ct)
                ?? throw new BusinessRuleException("Invalid coupon code.");
            var userRedemptions = await _db.CouponRedemptions.CountAsync(r => r.CouponId == coupon.Id && r.UserId == userId, ct);
            var (valid, message, computed) = CouponService.Evaluate(coupon, subtotal, userRedemptions);
            if (!valid) throw new BusinessRuleException(message);
            discount = computed;
        }

        // 5–6. Tax (D8 — default rate 0) over taxable lines; shipping free (D7).
        var taxRate = _config.GetValue<decimal?>("Tax:Rate") ?? 0m;
        var tax = taxRate > 0m ? Math.Round(taxableSubtotal * taxRate, 2) : 0m;
        const decimal shipping = 0m;
        var total = subtotal - discount + tax + shipping;

        // Customer snapshot from profile/identity, falling back to the shipping address.
        var profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var email = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        var customerName = profile is not null
            ? $"{profile.FirstName} {profile.LastName}".Trim()
            : $"{ship.FirstName} {ship.LastName}".Trim();

        // 7. Create the order awaiting admin confirmation, with line snapshots.
        var order = new Order
        {
            OrderNumber = await OrderingHelpers.GenerateOrderNumberAsync(_db, ct),
            UserId = userId,
            CustomerName = customerName,
            CustomerEmail = string.IsNullOrWhiteSpace(email) ? ship.Email : email!,
            Status = OrderStatus.AwaitingConfirmation,
            PaymentMethod = request.PaymentMethod,
            Currency = "USD",
            Subtotal = subtotal,
            DiscountAmount = discount,
            CouponCode = coupon?.Code,
            TaxAmount = tax,
            ShippingAmount = shipping,
            Total = total,
            CustomerNote = request.CustomerNote,
            ShipFirstName = ship.FirstName,
            ShipLastName = ship.LastName,
            ShipStreetAddress = ship.StreetAddress,
            ShipApartment = ship.Apartment,
            ShipCity = ship.City,
            ShipPhone = ship.Phone,
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

        // 8. Optionally persist the inline shipping address to the address book.
        if (request.SaveAddress && request.ShippingAddress is not null)
        {
            var isFirst = !await _db.Addresses.AnyAsync(a => a.UserId == userId, ct);
            _db.Addresses.Add(new Address
            {
                UserId = userId,
                FirstName = ship.FirstName,
                LastName = ship.LastName,
                StreetAddress = ship.StreetAddress,
                Apartment = ship.Apartment,
                City = ship.City,
                PhoneNumber = ship.Phone,
                Email = ship.Email,
                IsDefault = isFirst
            });
        }

        // 9. Reserve stock immediately (§7.4 — restored if cancelled/rejected/returned).
        foreach (var item in cart.Items)
        {
            item.ProductVariant.StockCount -= item.Quantity;
        }

        await _db.SaveChangesAsync(ct); // assigns order.Id before redemption is written.

        // 10. Record coupon usage.
        if (coupon is not null)
        {
            coupon.TimesUsed += 1;
            _db.CouponRedemptions.Add(new CouponRedemption
            {
                CouponId = coupon.Id,
                UserId = userId,
                OrderId = order.Id,
                UsedAt = DateTime.UtcNow
            });
        }

        // 11. Clear the cart.
        _db.CartItems.RemoveRange(cart.Items);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetMineAsync(userId, order.Id, ct);
    }

    public async Task<PagedResult<OrderSummaryDto>> ListMineAsync(string userId, OrderStatus? status, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var page = pageNumber < 1 ? 1 : pageNumber;
        var size = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        if (status is not null) query = query.Where(o => o.Status == status);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(o => o.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(o => new OrderSummaryDto(
                o.Id, o.OrderNumber, o.Status, o.PaymentMethod, o.Total, o.Items.Count, o.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryDto>(rows, page, size, total);
    }

    public async Task<OrderDto> GetMineAsync(string userId, int id, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");
        return OrderingHelpers.MapOrder(order);
    }

    public async Task<OrderDto> CancelAsync(string userId, int id, CancelOrderRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.Status != OrderStatus.AwaitingConfirmation)
        {
            throw new BusinessRuleException("Only an order awaiting confirmation can be cancelled.");
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelReason = request.Reason;
        await OrderingHelpers.RestoreStockAsync(_db, order, ct); // §7.4

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetMineAsync(userId, id, ct);
    }

    public async Task<OrderDto> RequestReturnAsync(string userId, int id, CreateReturnRequest request, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.Status is not (OrderStatus.InTransit or OrderStatus.Delivered))
        {
            throw new BusinessRuleException("A return can only be requested while an order is in transit or delivered.");
        }

        order.StatusBeforeReturn = order.Status;
        order.Status = OrderStatus.ReturnRequested;
        order.ReturnRequestedAt = DateTime.UtcNow;
        order.ReturnReason = request.Reason;

        await _db.SaveChangesAsync(ct);
        return await GetMineAsync(userId, id, ct);
    }

    // ---- helpers --------------------------------------------------------------

    private async Task<AddressSnap> ResolveShippingAsync(string userId, CheckoutRequest request, CancellationToken ct)
    {
        if (request.ShippingAddressId is int addrId)
        {
            var a = await _db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == addrId && x.UserId == userId, ct)
                ?? throw new NotFoundException("Shipping address not found.");
            return new AddressSnap(a.FirstName, a.LastName, a.StreetAddress, a.Apartment, a.City, a.PhoneNumber, a.Email);
        }

        if (request.ShippingAddress is { } s)
        {
            return new AddressSnap(s.FirstName, s.LastName, s.StreetAddress, s.Apartment, s.City, s.PhoneNumber, s.Email);
        }

        throw new ValidationException(new Dictionary<string, string[]>
        {
            ["shippingAddress"] = new[] { "Provide a shippingAddressId or an inline shippingAddress." }
        });
    }

    private sealed record AddressSnap(
        string FirstName, string LastName, string StreetAddress, string? Apartment, string City, string Phone, string Email);
}
