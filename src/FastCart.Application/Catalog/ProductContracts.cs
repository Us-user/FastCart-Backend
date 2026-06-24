using FastCart.Application.Common;
using FastCart.Domain.Enums;

namespace FastCart.Application.Catalog;

/// <summary>Products & variants (§6.5). Implemented in Infrastructure.</summary>
public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> ListAsync(ProductFilter filter, CancellationToken ct = default);
    Task<ProductDetailDto> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductListItemDto>> GetRelatedAsync(int id, int take, CancellationToken ct = default);
    Task<ProductDetailDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);

    // Admin management (§6.5)
    Task<ProductDetailDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
    Task<IReadOnlyList<ImageDto>> AddImagesAsync(int id, IReadOnlyList<ImageUpload> images, CancellationToken ct = default);
    Task DeleteImageAsync(int id, int imageId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminVariantDto>> ListVariantsAsync(int id, CancellationToken ct = default);
    Task<AdminVariantDto> AddVariantAsync(int id, AddVariantRequest request, CancellationToken ct = default);
    Task<AdminVariantDto> UpdateVariantAsync(int id, int variantId, UpdateVariantRequest request, CancellationToken ct = default);
    Task DeleteVariantAsync(int id, int variantId, CancellationToken ct = default);
    Task<AdminVariantDto> UpdateStockAsync(int id, int variantId, int count, CancellationToken ct = default);
    Task<OptionDto> AddOptionAsync(int id, OptionRequest request, CancellationToken ct = default);
    Task<OptionDto> UpdateOptionAsync(int id, int optionId, OptionRequest request, CancellationToken ct = default);
    Task DeleteOptionAsync(int id, int optionId, CancellationToken ct = default);
}

// ── Read: list card (§6.5) ──────────────────────────────────────────────────
// Init-property record so the EF projection can omit Swatches (stitched after).

public sealed record ProductListItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string BrandName { get; init; } = default!;
    public string CategoryName { get; init; } = default!;
    public string SubCategoryName { get; init; } = default!;
    public string? PrimaryImageUrl { get; init; }
    public decimal? FromPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool HasDiscount { get; init; }
    public bool InStock { get; init; }
    public ProductCondition? Condition { get; init; }
    public bool IsNew { get; init; }
    public double AvgRating { get; init; }
    public int ReviewCount { get; init; }
    public IReadOnlyList<ColorSwatchDto> Swatches { get; init; } = Array.Empty<ColorSwatchDto>();
}

public sealed record ColorSwatchDto(string Value, string? HexCode);

// ── Read: detail (§6.5) ─────────────────────────────────────────────────────

public sealed record ProductDetailDto(
    int Id,
    string Name,
    string? Code,
    string? Description,
    BrandDto Brand,
    int CategoryId,
    string CategoryName,
    int SubCategoryId,
    string SubCategoryName,
    ProductCondition? Condition,
    bool IsTaxable,
    DateTime CreatedAt,
    IReadOnlyList<ImageDto> Images,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<OptionDto> Options,
    IReadOnlyList<VariantDto> Variants,
    double AvgRating,
    int ReviewCount,
    decimal? FromPrice,
    decimal? MaxPrice,
    bool InStock);

public sealed record ImageDto(int Id, string Url, bool IsPrimary, int SortOrder);
public sealed record OptionDto(int Id, string Name, int SortOrder, IReadOnlyList<OptionValueDto> Values);
public sealed record OptionValueDto(int Id, string Value, int? ColorId, string? ColorHex, int SortOrder);
public sealed record VariantDto(
    int Id, string Sku, decimal Price, bool HasDiscount, decimal? DiscountPrice,
    decimal EffectivePrice, int StockCount, bool IsActive, IReadOnlyList<VariantOptionDto> Options);
public sealed record VariantOptionDto(string OptionName, string Value);

// ── Filtering / sorting / paging (§6.5) ─────────────────────────────────────

public sealed class ProductFilter
{
    public string? Q { get; init; }
    public int? CategoryId { get; init; }
    public int? SubCategoryId { get; init; }
    public List<int>? BrandIds { get; init; }
    public List<int>? ColorIds { get; init; }
    public List<int>? TagIds { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public ProductCondition? Condition { get; init; }
    public double? MinRating { get; init; }
    public bool? HasDiscount { get; init; }
    public bool? IsNew { get; init; }
    public bool? InStock { get; init; }
    /// <summary>newest | price_asc | price_desc | popularity | rating.</summary>
    public string? Sort { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// ── Create (§6.5) — one call with options + values + variants + images ──────

public sealed record CreateProductRequest
{
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public int SubCategoryId { get; init; }
    public int BrandId { get; init; }
    public bool IsTaxable { get; init; }
    public ProductCondition? Condition { get; init; }
    public List<int> TagIds { get; init; } = new();
    public List<CreateOptionInput> Options { get; init; } = new();
    public List<CreateVariantInput> Variants { get; init; } = new();
    public List<ImageUpload> Images { get; init; } = new();
}

public sealed record CreateOptionInput
{
    public string Name { get; init; } = default!;
    public int SortOrder { get; init; }
    public List<CreateOptionValueInput> Values { get; init; } = new();
}

public sealed record CreateOptionValueInput
{
    public string Value { get; init; } = default!;
    public int? ColorId { get; init; }
    public int SortOrder { get; init; }
}

public sealed record CreateVariantInput
{
    public string Sku { get; init; } = default!;
    public List<VariantOptionValueInput> OptionValues { get; init; } = new();
    public decimal Price { get; init; }
    public bool HasDiscount { get; init; }
    public decimal? DiscountPrice { get; init; }
    public decimal CostPrice { get; init; }
    public int Count { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record VariantOptionValueInput
{
    public string OptionName { get; init; } = default!;
    public string Value { get; init; } = default!;
}

/// <summary>An image to store (stream + metadata), kept free of ASP.NET IFormFile.</summary>
public sealed record ImageUpload(Stream Content, string FileName, string ContentType);

// ── Admin management (§6.5) ─────────────────────────────────────────────────

/// <summary>Product-level update — all fields optional (§6.5).</summary>
public sealed record UpdateProductRequest
{
    public string? Name { get; init; }
    public string? Code { get; init; }
    public string? Description { get; init; }
    public int? SubCategoryId { get; init; }
    public int? BrandId { get; init; }
    public bool? IsTaxable { get; init; }
    public ProductCondition? Condition { get; init; }
    public List<int>? TagIds { get; init; }
}

/// <summary>Variant projection for admin — includes CostPrice (hidden from public detail).</summary>
public sealed record AdminVariantDto(
    int Id, string Sku, decimal Price, bool HasDiscount, decimal? DiscountPrice, decimal EffectivePrice,
    decimal CostPrice, int StockCount, bool IsActive, IReadOnlyList<VariantOptionDto> Options);

public sealed record AddVariantRequest
{
    public string Sku { get; init; } = default!;

    /// <summary>One existing option-value id per option axis of the product (§6.5/§8).</summary>
    public List<int> OptionValueIds { get; init; } = new();
    public decimal Price { get; init; }
    public bool HasDiscount { get; init; }
    public decimal? DiscountPrice { get; init; }
    public decimal CostPrice { get; init; }
    public int Count { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record UpdateVariantRequest
{
    public string? Sku { get; init; }
    public decimal? Price { get; init; }
    public bool? HasDiscount { get; init; }
    public decimal? DiscountPrice { get; init; }
    public decimal? CostPrice { get; init; }
    public int? Count { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record StockUpdateRequest
{
    public int Count { get; init; }
}

/// <summary>Add/update an option axis with its values (§6.5, D1/D13).</summary>
public sealed record OptionRequest
{
    public string Name { get; init; } = default!;
    public int SortOrder { get; init; }
    public List<CreateOptionValueInput> Values { get; init; } = new();
}
