using FastCart.Domain.Common;

namespace FastCart.Domain.Entities;

/// <summary>Top-level catalog category (§5.2).</summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? ImageUrl { get; set; }

    public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
}

/// <summary>Category → SubCategory hierarchy (§5.2). Products hang off subcategories.</summary>
public class SubCategory : BaseEntity
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = default!;
    public string Name { get; set; } = default!;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>Product brand (§5.2).</summary>
public class Brand : BaseEntity
{
    public string Name { get; set; } = default!;
}

/// <summary>Colour with hex code for swatches (§5.2).</summary>
public class Color : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? HexCode { get; set; }
}

/// <summary>Free-form product tag (§5.2).</summary>
public class Tag : BaseEntity
{
    public string Name { get; set; } = default!;
}
