using FastCart.Application.Orders;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Orders;

/// <summary>Shared order helpers used by both the customer and admin order services.</summary>
internal static class OrderingHelpers
{
    /// <summary>Project a fully-loaded order (with <c>Items</c>) to its DTO.</summary>
    public static OrderDto MapOrder(Order o) => new(
        o.Id, o.OrderNumber, o.Status, o.PaymentStatus, o.PaymentMethod, o.Currency,
        o.Subtotal, o.DiscountAmount, o.CouponCode, o.TaxAmount, o.ShippingAmount, o.Total,
        o.CustomerName, o.CustomerEmail, o.CustomerNote,
        new AddressSnapshotDto(o.ShipFirstName, o.ShipLastName, o.ShipStreetAddress, o.ShipApartment, o.ShipCity, o.ShipPhone, o.ShipEmail),
        o.BillFirstName is null
            ? null
            : new AddressSnapshotDto(o.BillFirstName, o.BillLastName!, o.BillStreetAddress!, o.BillApartment, o.BillCity!, o.BillPhone!, o.BillEmail!),
        o.CancelledAt, o.CancelReason, o.CreatedAt,
        o.Items.OrderBy(i => i.Id).Select(i => new OrderItemDto(
            i.Id, i.ProductId, i.ProductVariantId, i.ProductName, i.Sku, i.VariantDescription, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());

    /// <summary>Human-friendly, unique order number (e.g. <c>#125128</c>, §5.5).</summary>
    public static async Task<string> GenerateOrderNumberAsync(AppDbContext db, CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var candidate = "#" + Random.Shared.Next(100000, 1_000_000);
            if (!await db.Orders.AnyAsync(o => o.OrderNumber == candidate, ct))
            {
                return candidate;
            }
        }
        return "#" + DateTime.UtcNow.Ticks; // effectively unique fallback.
    }

    /// <summary>Snapshot string for a variant, e.g. "Size: M / Colour: Red" (§7.1).</summary>
    public static string? DescribeVariant(ProductVariant v)
    {
        var desc = string.Join(" / ", v.OptionValues
            .OrderBy(ov => ov.ProductOptionValue.ProductOption.SortOrder)
            .Select(ov => $"{ov.ProductOptionValue.ProductOption.Name}: {ov.ProductOptionValue.Value}"));
        return string.IsNullOrEmpty(desc) ? null : desc;
    }

    /// <summary>Restore per-variant stock for an order's lines (cancel/return, §7.4).</summary>
    public static async Task RestoreStockAsync(AppDbContext db, Order order, CancellationToken ct)
    {
        var variantIds = order.Items
            .Where(i => i.ProductVariantId != null)
            .Select(i => i.ProductVariantId!.Value)
            .Distinct()
            .ToList();
        if (variantIds.Count == 0) return;

        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync(ct);
        foreach (var item in order.Items)
        {
            if (item.ProductVariantId is int vid)
            {
                var v = variants.FirstOrDefault(x => x.Id == vid);
                if (v is not null) v.StockCount += item.Quantity;
            }
        }
    }
}
