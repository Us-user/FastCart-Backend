using FastCart.Application.Catalog;
using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Common.Interfaces;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Catalog;

internal static class Paging
{
    public static (int page, int size) Normalize(int pageNumber, int pageSize) =>
        (pageNumber < 1 ? 1 : pageNumber, pageSize is < 1 or > 100 ? 20 : pageSize);
}

/// <summary>Category CRUD with multipart image upload (§6.4).</summary>
public sealed class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;

    public CategoryService(AppDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeSubcategories, CancellationToken ct = default)
    {
        var query = _db.Categories.AsNoTracking().OrderBy(c => c.Name);

        if (includeSubcategories)
        {
            return await query
                .Select(c => new CategoryDto(c.Id, c.Name, c.ImageUrl,
                    c.SubCategories.OrderBy(s => s.Name)
                        .Select(s => new SubCategoryDto(s.Id, s.CategoryId, s.Name)).ToList()))
                .ToListAsync(ct);
        }

        return await query
            .Select(c => new CategoryDto(c.Id, c.Name, c.ImageUrl, null))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.ImageUrl,
                c.SubCategories.OrderBy(s => s.Name)
                    .Select(s => new SubCategoryDto(s.Id, s.CategoryId, s.Name)).ToList()))
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException("Category not found.");
    }

    public async Task<CategoryDto> CreateAsync(CategoryInput input, CancellationToken ct = default)
    {
        var category = new Category { Name = input.Name };
        if (input.ImageContent is not null)
        {
            category.ImageUrl = await _storage.SaveAsync(
                input.ImageContent, input.ImageFileName ?? "category", input.ImageContentType ?? "application/octet-stream", "categories", ct);
        }

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.ImageUrl, null);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryInput input, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found.");

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            category.Name = input.Name;
        }

        if (input.ImageContent is not null)
        {
            category.ImageUrl = await _storage.SaveAsync(
                input.ImageContent, input.ImageFileName ?? "category", input.ImageContentType ?? "application/octet-stream", "categories", ct);
        }

        await _db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.ImageUrl, null);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found.");
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>SubCategory CRUD (§6.4).</summary>
public sealed class SubCategoryService : ISubCategoryService
{
    private readonly AppDbContext _db;

    public SubCategoryService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SubCategoryDto>> ListAsync(int? categoryId, CancellationToken ct = default)
    {
        var query = _db.SubCategories.AsNoTracking();
        if (categoryId is not null)
        {
            query = query.Where(s => s.CategoryId == categoryId);
        }

        return await query.OrderBy(s => s.Name)
            .Select(s => new SubCategoryDto(s.Id, s.CategoryId, s.Name))
            .ToListAsync(ct);
    }

    public async Task<SubCategoryDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.SubCategories.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SubCategoryDto(s.Id, s.CategoryId, s.Name))
            .FirstOrDefaultAsync(ct);
        return dto ?? throw new NotFoundException("Subcategory not found.");
    }

    public async Task<SubCategoryDto> CreateAsync(SubCategoryRequest request, CancellationToken ct = default)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new NotFoundException("Category not found.");
        }

        var entity = new SubCategory { CategoryId = request.CategoryId, Name = request.Name };
        _db.SubCategories.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new SubCategoryDto(entity.Id, entity.CategoryId, entity.Name);
    }

    public async Task<SubCategoryDto> UpdateAsync(int id, SubCategoryRequest request, CancellationToken ct = default)
    {
        var entity = await _db.SubCategories.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Subcategory not found.");

        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new NotFoundException("Category not found.");
        }

        entity.CategoryId = request.CategoryId;
        entity.Name = request.Name;
        await _db.SaveChangesAsync(ct);
        return new SubCategoryDto(entity.Id, entity.CategoryId, entity.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.SubCategories.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Subcategory not found.");
        _db.SubCategories.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Brand CRUD + search (§6.4).</summary>
public sealed class BrandService : IBrandService
{
    private readonly AppDbContext _db;

    public BrandService(AppDbContext db) => _db = db;

    public async Task<PagedResult<BrandDto>> ListAsync(string? brandName, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var (page, size) = Paging.Normalize(pageNumber, pageSize);
        var query = _db.Brands.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            query = query.Where(b => EF.Functions.ILike(b.Name, $"%{brandName}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(b => b.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(b => new BrandDto(b.Id, b.Name))
            .ToListAsync(ct);

        return new PagedResult<BrandDto>(items, page, size, total);
    }

    public async Task<BrandDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Brands.AsNoTracking().Where(b => b.Id == id)
            .Select(b => new BrandDto(b.Id, b.Name)).FirstOrDefaultAsync(ct);
        return dto ?? throw new NotFoundException("Brand not found.");
    }

    public async Task<BrandDto> CreateAsync(BrandRequest request, CancellationToken ct = default)
    {
        var entity = new Brand { Name = request.Name };
        _db.Brands.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new BrandDto(entity.Id, entity.Name);
    }

    public async Task<BrandDto> UpdateAsync(int id, BrandRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Brand not found.");
        entity.Name = request.Name;
        await _db.SaveChangesAsync(ct);
        return new BrandDto(entity.Id, entity.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Brand not found.");
        _db.Brands.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Color CRUD + search (§6.4).</summary>
public sealed class ColorService : IColorService
{
    private readonly AppDbContext _db;

    public ColorService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ColorDto>> ListAsync(string? colorName, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var (page, size) = Paging.Normalize(pageNumber, pageSize);
        var query = _db.Colors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(colorName))
        {
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{colorName}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(c => new ColorDto(c.Id, c.Name, c.HexCode))
            .ToListAsync(ct);

        return new PagedResult<ColorDto>(items, page, size, total);
    }

    public async Task<ColorDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Colors.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new ColorDto(c.Id, c.Name, c.HexCode)).FirstOrDefaultAsync(ct);
        return dto ?? throw new NotFoundException("Color not found.");
    }

    public async Task<ColorDto> CreateAsync(ColorRequest request, CancellationToken ct = default)
    {
        var entity = new Color { Name = request.Name, HexCode = request.HexCode };
        _db.Colors.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new ColorDto(entity.Id, entity.Name, entity.HexCode);
    }

    public async Task<ColorDto> UpdateAsync(int id, ColorRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Colors.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Color not found.");
        entity.Name = request.Name;
        entity.HexCode = request.HexCode;
        await _db.SaveChangesAsync(ct);
        return new ColorDto(entity.Id, entity.Name, entity.HexCode);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Colors.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Color not found.");
        _db.Colors.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Tag CRUD (§6.4).</summary>
public sealed class TagService : ITagService
{
    private readonly AppDbContext _db;

    public TagService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Tags.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name)).ToListAsync(ct);

    public async Task<TagDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Tags.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new TagDto(t.Id, t.Name)).FirstOrDefaultAsync(ct);
        return dto ?? throw new NotFoundException("Tag not found.");
    }

    public async Task<TagDto> CreateAsync(TagRequest request, CancellationToken ct = default)
    {
        var entity = new Tag { Name = request.Name };
        _db.Tags.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new TagDto(entity.Id, entity.Name);
    }

    public async Task<TagDto> UpdateAsync(int id, TagRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Tag not found.");
        entity.Name = request.Name;
        await _db.SaveChangesAsync(ct);
        return new TagDto(entity.Id, entity.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Tag not found.");
        _db.Tags.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
