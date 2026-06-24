using System.ComponentModel.DataAnnotations;
using FastCart.Application.Common;

namespace FastCart.Application.Catalog;

// ── Categories (§6.4) ───────────────────────────────────────────────────────

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeSubcategories, CancellationToken ct = default);
    Task<CategoryDto> GetAsync(int id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CategoryInput input, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, CategoryInput input, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record CategoryDto(int Id, string Name, string? ImageUrl, IReadOnlyList<SubCategoryDto>? SubCategories);

/// <summary>Category create/update — name plus optional image stream (multipart, §6.4).</summary>
public sealed record CategoryInput
{
    public string Name { get; init; } = default!;
    public Stream? ImageContent { get; init; }
    public string? ImageFileName { get; init; }
    public string? ImageContentType { get; init; }
}

// ── SubCategories (§6.4) ────────────────────────────────────────────────────

public interface ISubCategoryService
{
    Task<IReadOnlyList<SubCategoryDto>> ListAsync(int? categoryId, CancellationToken ct = default);
    Task<SubCategoryDto> GetAsync(int id, CancellationToken ct = default);
    Task<SubCategoryDto> CreateAsync(SubCategoryRequest request, CancellationToken ct = default);
    Task<SubCategoryDto> UpdateAsync(int id, SubCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record SubCategoryDto(int Id, int CategoryId, string Name);

public sealed record SubCategoryRequest
{
    [Required]
    public int CategoryId { get; init; }

    [Required, StringLength(100)]
    public string Name { get; init; } = default!;
}

// ── Brands (§6.4) ───────────────────────────────────────────────────────────

public interface IBrandService
{
    Task<PagedResult<BrandDto>> ListAsync(string? brandName, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<BrandDto> GetAsync(int id, CancellationToken ct = default);
    Task<BrandDto> CreateAsync(BrandRequest request, CancellationToken ct = default);
    Task<BrandDto> UpdateAsync(int id, BrandRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record BrandDto(int Id, string Name);

public sealed record BrandRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = default!;
}

// ── Colors (§6.4) ───────────────────────────────────────────────────────────

public interface IColorService
{
    Task<PagedResult<ColorDto>> ListAsync(string? colorName, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<ColorDto> GetAsync(int id, CancellationToken ct = default);
    Task<ColorDto> CreateAsync(ColorRequest request, CancellationToken ct = default);
    Task<ColorDto> UpdateAsync(int id, ColorRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record ColorDto(int Id, string Name, string? HexCode);

public sealed record ColorRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = default!;

    [StringLength(9)]
    public string? HexCode { get; init; }
}

// ── Tags (§6.4) ─────────────────────────────────────────────────────────────

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken ct = default);
    Task<TagDto> GetAsync(int id, CancellationToken ct = default);
    Task<TagDto> CreateAsync(TagRequest request, CancellationToken ct = default);
    Task<TagDto> UpdateAsync(int id, TagRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record TagDto(int Id, string Name);

public sealed record TagRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = default!;
}
