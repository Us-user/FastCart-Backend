using FastCart.Domain.Common;
using FastCart.Domain.Enums;

namespace FastCart.Domain.Entities;

/// <summary>
/// Product — shared attributes including price. Stock lives on variants; sizes/colours
/// are option values on variants. <see cref="BaseEntity.CreatedAt"/> drives the NEW badge.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? Description { get; set; }

    public int SubCategoryId { get; set; }
    public SubCategory SubCategory { get; set; } = default!;

    public int BrandId { get; set; }
    public Brand Brand { get; set; } = default!;

    public bool IsTaxable { get; set; }
    public ProductCondition? Condition { get; set; }

    public decimal Price { get; set; }
    public bool HasDiscount { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal CostPrice { get; set; }

    /// <summary>Effective price = DiscountPrice when HasDiscount, else Price.</summary>
    public decimal EffectivePrice => HasDiscount && DiscountPrice.HasValue ? DiscountPrice.Value : Price;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    public ICollection<ProductOption> Options { get; set; } = new List<ProductOption>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

/// <summary>Product image stored in object storage; DB holds the URL only (§5.2, D12).</summary>
public class ProductImage : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string Url { get; set; } = default!;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Product ↔ Tag join (§5.2).</summary>
public class ProductTag
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = default!;
}

/// <summary>An option axis for a product, e.g. "Size", "Colour", "Weight" (§5.2, D1).</summary>
public class ProductOption : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int SortOrder { get; set; }

    public ICollection<ProductOptionValue> Values { get; set; } = new List<ProductOptionValue>();
}

/// <summary>A value within an option axis, e.g. "M", "Red", "10kg" (§5.2).</summary>
public class ProductOptionValue : BaseEntity
{
    public int ProductOptionId { get; set; }
    public ProductOption ProductOption { get; set; } = default!;
    public string Value { get; set; } = default!;

    /// <summary>Optional colour swatch/hex for colour-type values (§5.2).</summary>
    public int? ColorId { get; set; }
    public Color? Color { get; set; }
    public int SortOrder { get; set; }

    public ICollection<ProductVariantOptionValue> VariantLinks { get; set; } = new List<ProductVariantOptionValue>();
}

/// <summary>
/// One sellable combination of option values — carries stock only (§5.2, D1).
/// Price lives on the parent Product. Not every possible combination need exist (D13).
/// </summary>
public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public int StockCount { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductVariantOptionValue> OptionValues { get; set; } = new List<ProductVariantOptionValue>();
}

/// <summary>
/// Links a variant to exactly one value per option axis (§5.2). The set of values
/// is unique per product.
/// </summary>
public class ProductVariantOptionValue
{
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;
    public int ProductOptionValueId { get; set; }
    public ProductOptionValue ProductOptionValue { get; set; } = default!;
}
