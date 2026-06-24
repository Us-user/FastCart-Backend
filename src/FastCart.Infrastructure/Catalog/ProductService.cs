using FastCart.Application.Catalog;
using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Common.Interfaces;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Catalog;

/// <summary>
/// Products & variants (§6.5). List/detail are variant-aware: fromPrice = min effective
/// price over active variants, inStock = any active variant with stock, swatches = colour
/// option values present. Effective price = DiscountPrice when HasDiscount, else Price.
/// </summary>
public sealed class ProductService : IProductService
{
    private const int NewArrivalDays = 30;
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;

    public ProductService(AppDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<PagedResult<ProductListItemDto>> ListAsync(ProductFilter filter, CancellationToken ct = default)
    {
        var (page, size) = Paging.Normalize(filter.PageNumber, filter.PageSize);
        var newCutoff = DateTime.UtcNow.AddDays(-NewArrivalDays);

        var query = ApplyFilters(_db.Products.AsNoTracking(), filter, newCutoff);
        var total = await query.CountAsync(ct);

        query = ApplySort(query, filter.Sort);

        var items = await ProjectCards(query.Skip((page - 1) * size).Take(size), newCutoff).ToListAsync(ct);
        items = await StitchSwatchesAsync(items, ct);

        return new PagedResult<ProductListItemDto>(items, page, size, total);
    }

    public async Task<ProductDetailDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetailDto(
                p.Id, p.Name, p.Code, p.Description,
                new BrandDto(p.Brand.Id, p.Brand.Name),
                p.SubCategory.CategoryId, p.SubCategory.Category.Name,
                p.SubCategoryId, p.SubCategory.Name,
                p.Condition, p.IsTaxable, p.CreatedAt,
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                    .Select(i => new ImageDto(i.Id, i.Url, i.IsPrimary, i.SortOrder)).ToList(),
                p.ProductTags.Select(pt => new TagDto(pt.Tag.Id, pt.Tag.Name)).ToList(),
                p.Options.OrderBy(o => o.SortOrder).Select(o => new OptionDto(o.Id, o.Name, o.SortOrder,
                    o.Values.OrderBy(v => v.SortOrder)
                        .Select(v => new OptionValueDto(v.Id, v.Value, v.ColorId, v.Color!.HexCode, v.SortOrder)).ToList())).ToList(),
                p.Variants.OrderBy(v => v.Id).Select(v => new VariantDto(
                    v.Id, v.Sku, v.Price, v.HasDiscount, v.DiscountPrice,
                    v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price,
                    v.StockCount, v.IsActive,
                    v.OptionValues.Select(ov => new VariantOptionDto(ov.ProductOptionValue.ProductOption.Name, ov.ProductOptionValue.Value)).ToList())).ToList(),
                p.Reviews.Average(r => (double?)r.Rating) ?? 0,
                p.Reviews.Count(),
                p.Variants.Where(v => v.IsActive).Min(v => (decimal?)(v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price)),
                p.Variants.Where(v => v.IsActive).Max(v => (decimal?)(v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price)),
                p.Variants.Any(v => v.IsActive && v.StockCount > 0)))
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException("Product not found.");
    }

    public async Task<IReadOnlyList<ProductListItemDto>> GetRelatedAsync(int id, int take, CancellationToken ct = default)
    {
        var subCategoryId = await _db.Products.AsNoTracking()
            .Where(p => p.Id == id).Select(p => (int?)p.SubCategoryId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product not found.");

        var newCutoff = DateTime.UtcNow.AddDays(-NewArrivalDays);
        take = take is < 1 or > 50 ? 8 : take;

        var query = _db.Products.AsNoTracking()
            .Where(p => p.SubCategoryId == subCategoryId && p.Id != id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(take);

        var items = await ProjectCards(query, newCutoff).ToListAsync(ct);
        return await StitchSwatchesAsync(items, ct);
    }

    public async Task<ProductDetailDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        if (request.Variants.Count == 0)
        {
            throw new BusinessRuleException("A product must have at least one variant.");
        }

        if (!await _db.SubCategories.AnyAsync(s => s.Id == request.SubCategoryId, ct))
        {
            throw new NotFoundException("Subcategory not found.");
        }

        if (!await _db.Brands.AnyAsync(b => b.Id == request.BrandId, ct))
        {
            throw new NotFoundException("Brand not found.");
        }

        // SKU uniqueness — within the request and against existing variants.
        var skus = request.Variants.Select(v => v.Sku).ToList();
        if (skus.Count != skus.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new ConflictException("Duplicate SKUs in the request.");
        }
        if (await _db.ProductVariants.AnyAsync(v => skus.Contains(v.Sku), ct))
        {
            throw new ConflictException("One or more SKUs already exist.");
        }

        // No two variants may share the same option-value combination (§5.2).
        var combos = new HashSet<string>();
        foreach (var v in request.Variants)
        {
            var signature = string.Join("|", v.OptionValues
                .Select(o => $"{o.OptionName}={o.Value}").OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            if (!combos.Add(signature))
            {
                throw new ConflictException($"Duplicate variant option combination (SKU '{v.Sku}').");
            }
        }

        var product = new Product
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            SubCategoryId = request.SubCategoryId,
            BrandId = request.BrandId,
            IsTaxable = request.IsTaxable,
            Condition = request.Condition
        };

        // Options + values, indexed by (optionName, value) for variant linking.
        var valueLookup = new Dictionary<(string Option, string Value), ProductOptionValue>();
        foreach (var optionInput in request.Options)
        {
            var option = new ProductOption { Name = optionInput.Name, SortOrder = optionInput.SortOrder };
            foreach (var valueInput in optionInput.Values)
            {
                var value = new ProductOptionValue
                {
                    Value = valueInput.Value,
                    ColorId = valueInput.ColorId,
                    SortOrder = valueInput.SortOrder
                };
                option.Values.Add(value);
                valueLookup[(optionInput.Name, valueInput.Value)] = value;
            }
            product.Options.Add(option);
        }

        var optionNames = request.Options.Select(o => o.Name).ToHashSet();

        foreach (var variantInput in request.Variants)
        {
            // Each variant must specify exactly one value per option axis (§8).
            var variantOptionNames = variantInput.OptionValues.Select(ov => ov.OptionName).ToHashSet();
            if (!variantOptionNames.SetEquals(optionNames))
            {
                throw new BusinessRuleException(
                    $"Variant '{variantInput.Sku}' must specify exactly one value for each option axis.");
            }

            var variant = new ProductVariant
            {
                Sku = variantInput.Sku,
                Price = variantInput.Price,
                HasDiscount = variantInput.HasDiscount,
                DiscountPrice = variantInput.DiscountPrice,
                CostPrice = variantInput.CostPrice,
                StockCount = variantInput.Count,
                IsActive = variantInput.IsActive
            };

            foreach (var ov in variantInput.OptionValues)
            {
                if (!valueLookup.TryGetValue((ov.OptionName, ov.Value), out var optionValue))
                {
                    throw new BusinessRuleException(
                        $"Variant '{variantInput.Sku}' references unknown option value '{ov.OptionName}: {ov.Value}'.");
                }
                variant.OptionValues.Add(new ProductVariantOptionValue { ProductOptionValue = optionValue });
            }

            product.Variants.Add(variant);
        }

        // Tags (link existing only).
        if (request.TagIds.Count > 0)
        {
            var tagIds = await _db.Tags.Where(t => request.TagIds.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
            foreach (var tagId in tagIds)
            {
                product.ProductTags.Add(new ProductTag { TagId = tagId });
            }
        }

        // Images → storage; first image is primary unless otherwise set.
        for (var i = 0; i < request.Images.Count; i++)
        {
            var img = request.Images[i];
            var url = await _storage.SaveAsync(img.Content, img.FileName, img.ContentType, "products", ct);
            product.Images.Add(new ProductImage { Url = url, IsPrimary = i == 0, SortOrder = i });
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(product.Id, ct);
    }

    public async Task<ProductDetailDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.ProductTags)
            .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product not found.");

        if (request.SubCategoryId is not null)
        {
            if (!await _db.SubCategories.AnyAsync(s => s.Id == request.SubCategoryId, ct))
                throw new NotFoundException("Subcategory not found.");
            product.SubCategoryId = request.SubCategoryId.Value;
        }
        if (request.BrandId is not null)
        {
            if (!await _db.Brands.AnyAsync(b => b.Id == request.BrandId, ct))
                throw new NotFoundException("Brand not found.");
            product.BrandId = request.BrandId.Value;
        }
        if (request.Name is not null) product.Name = request.Name;
        if (request.Code is not null) product.Code = request.Code;
        if (request.Description is not null) product.Description = request.Description;
        if (request.IsTaxable is not null) product.IsTaxable = request.IsTaxable.Value;
        if (request.Condition is not null) product.Condition = request.Condition;

        if (request.TagIds is not null)
        {
            product.ProductTags.Clear();
            var tagIds = await _db.Tags.Where(t => request.TagIds.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
            foreach (var tagId in tagIds) product.ProductTags.Add(new ProductTag { TagId = tagId });
        }

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product not found.");
        var urls = product.Images.Select(i => i.Url).ToList();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);
        foreach (var url in urls) await _storage.DeleteAsync(url, ct);
    }

    public async Task BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        var products = await _db.Products.Include(p => p.Images).Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var urls = products.SelectMany(p => p.Images.Select(i => i.Url)).ToList();
        _db.Products.RemoveRange(products);
        await _db.SaveChangesAsync(ct);
        foreach (var url in urls) await _storage.DeleteAsync(url, ct);
    }

    public async Task<IReadOnlyList<ImageDto>> AddImagesAsync(int id, IReadOnlyList<ImageUpload> images, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product not found.");

        var nextSort = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1;
        var anyPrimary = product.Images.Any(i => i.IsPrimary);
        var added = new List<ProductImage>();

        foreach (var img in images)
        {
            var url = await _storage.SaveAsync(img.Content, img.FileName, img.ContentType, "products", ct);
            var entity = new ProductImage { ProductId = id, Url = url, SortOrder = nextSort++, IsPrimary = !anyPrimary };
            anyPrimary = true;
            product.Images.Add(entity);
            added.Add(entity);
        }

        await _db.SaveChangesAsync(ct);
        return added.Select(i => new ImageDto(i.Id, i.Url, i.IsPrimary, i.SortOrder)).ToList();
    }

    public async Task DeleteImageAsync(int id, int imageId, CancellationToken ct = default)
    {
        var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == id, ct)
            ?? throw new NotFoundException("Image not found.");
        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync(ct);
        await _storage.DeleteAsync(image.Url, ct);
    }

    public async Task<IReadOnlyList<AdminVariantDto>> ListVariantsAsync(int id, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("Product not found.");
        return await _db.ProductVariants.AsNoTracking().Where(v => v.ProductId == id).OrderBy(v => v.Id)
            .Select(v => new AdminVariantDto(v.Id, v.Sku, v.Price, v.HasDiscount, v.DiscountPrice,
                v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price,
                v.CostPrice, v.StockCount, v.IsActive,
                v.OptionValues.Select(ov => new VariantOptionDto(ov.ProductOptionValue.ProductOption.Name, ov.ProductOptionValue.Value)).ToList()))
            .ToListAsync(ct);
    }

    public async Task<AdminVariantDto> AddVariantAsync(int id, AddVariantRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product not found.");

        if (await _db.ProductVariants.AnyAsync(v => v.Sku == request.Sku, ct))
            throw new ConflictException("SKU already exists.");

        var values = await _db.ProductOptionValues
            .Where(v => request.OptionValueIds.Contains(v.Id) && v.ProductOption.ProductId == id)
            .Select(v => new { v.Id, v.ProductOptionId })
            .ToListAsync(ct);

        if (values.Count != request.OptionValueIds.Distinct().Count())
            throw new BusinessRuleException("One or more option values are invalid for this product.");

        var coveredAxes = values.Select(v => v.ProductOptionId).ToHashSet();
        if (!coveredAxes.SetEquals(product.Options.Select(o => o.Id)) || coveredAxes.Count != values.Count)
            throw new BusinessRuleException("A variant must specify exactly one value per option axis.");

        // Reject a combination that an existing variant already covers (§5.2).
        var newValueIds = request.OptionValueIds.ToHashSet();
        var existingCombos = await _db.ProductVariants
            .Where(v => v.ProductId == id)
            .Select(v => v.OptionValues.Select(ov => ov.ProductOptionValueId).ToList())
            .ToListAsync(ct);
        if (existingCombos.Any(c => c.Count == newValueIds.Count && newValueIds.SetEquals(c)))
        {
            throw new ConflictException("A variant with this option combination already exists.");
        }

        var variant = new ProductVariant
        {
            ProductId = id,
            Sku = request.Sku,
            Price = request.Price,
            HasDiscount = request.HasDiscount,
            DiscountPrice = request.DiscountPrice,
            CostPrice = request.CostPrice,
            StockCount = request.Count,
            IsActive = request.IsActive
        };
        foreach (var v in values) variant.OptionValues.Add(new ProductVariantOptionValue { ProductOptionValueId = v.Id });

        _db.ProductVariants.Add(variant);
        await _db.SaveChangesAsync(ct);
        return await GetAdminVariantAsync(variant.Id, ct);
    }

    public async Task<AdminVariantDto> UpdateVariantAsync(int id, int variantId, UpdateVariantRequest request, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == id, ct)
            ?? throw new NotFoundException("Variant not found.");

        if (request.Sku is not null && request.Sku != variant.Sku)
        {
            if (await _db.ProductVariants.AnyAsync(v => v.Sku == request.Sku && v.Id != variantId, ct))
                throw new ConflictException("SKU already exists.");
            variant.Sku = request.Sku;
        }
        if (request.Price is not null) variant.Price = request.Price.Value;
        if (request.HasDiscount is not null) variant.HasDiscount = request.HasDiscount.Value;
        if (request.DiscountPrice is not null) variant.DiscountPrice = request.DiscountPrice;
        if (request.CostPrice is not null) variant.CostPrice = request.CostPrice.Value;
        if (request.Count is not null) variant.StockCount = request.Count.Value;
        if (request.IsActive is not null) variant.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(ct);
        return await GetAdminVariantAsync(variantId, ct);
    }

    public async Task DeleteVariantAsync(int id, int variantId, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == id, ct)
            ?? throw new NotFoundException("Variant not found.");
        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AdminVariantDto> UpdateStockAsync(int id, int variantId, int count, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == id, ct)
            ?? throw new NotFoundException("Variant not found.");
        variant.StockCount = count < 0 ? 0 : count;
        await _db.SaveChangesAsync(ct);
        return await GetAdminVariantAsync(variantId, ct);
    }

    public async Task<OptionDto> AddOptionAsync(int id, OptionRequest request, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("Product not found.");
        var option = new ProductOption { ProductId = id, Name = request.Name, SortOrder = request.SortOrder };
        foreach (var v in request.Values)
            option.Values.Add(new ProductOptionValue { Value = v.Value, ColorId = v.ColorId, SortOrder = v.SortOrder });
        _db.ProductOptions.Add(option);
        await _db.SaveChangesAsync(ct);
        return await GetOptionAsync(option.Id, ct);
    }

    public async Task<OptionDto> UpdateOptionAsync(int id, int optionId, OptionRequest request, CancellationToken ct = default)
    {
        var option = await _db.ProductOptions.Include(o => o.Values)
            .FirstOrDefaultAsync(o => o.Id == optionId && o.ProductId == id, ct) ?? throw new NotFoundException("Option not found.");

        option.Name = request.Name;
        option.SortOrder = request.SortOrder;

        var existing = option.Values.Select(v => v.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var v in request.Values)
        {
            if (!existing.Contains(v.Value))
                option.Values.Add(new ProductOptionValue { Value = v.Value, ColorId = v.ColorId, SortOrder = v.SortOrder });
        }

        await _db.SaveChangesAsync(ct);
        return await GetOptionAsync(optionId, ct);
    }

    public async Task DeleteOptionAsync(int id, int optionId, CancellationToken ct = default)
    {
        var option = await _db.ProductOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.ProductId == id, ct)
            ?? throw new NotFoundException("Option not found.");

        var inUse = await _db.ProductVariantOptionValues.AnyAsync(link => link.ProductOptionValue.ProductOptionId == optionId, ct);
        if (inUse) throw new ConflictException("Cannot delete an option that variants still reference.");

        _db.ProductOptions.Remove(option);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AdminVariantDto> GetAdminVariantAsync(int variantId, CancellationToken ct) =>
        await _db.ProductVariants.AsNoTracking().Where(v => v.Id == variantId)
            .Select(v => new AdminVariantDto(v.Id, v.Sku, v.Price, v.HasDiscount, v.DiscountPrice,
                v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price,
                v.CostPrice, v.StockCount, v.IsActive,
                v.OptionValues.Select(ov => new VariantOptionDto(ov.ProductOptionValue.ProductOption.Name, ov.ProductOptionValue.Value)).ToList()))
            .FirstAsync(ct);

    private async Task<OptionDto> GetOptionAsync(int optionId, CancellationToken ct) =>
        await _db.ProductOptions.AsNoTracking().Where(o => o.Id == optionId)
            .Select(o => new OptionDto(o.Id, o.Name, o.SortOrder,
                o.Values.OrderBy(v => v.SortOrder).Select(v => new OptionValueDto(v.Id, v.Value, v.ColorId, v.Color!.HexCode, v.SortOrder)).ToList()))
            .FirstAsync(ct);

    // ── helpers ─────────────────────────────────────────────────────────────

    private IQueryable<Product> ApplyFilters(IQueryable<Product> query, ProductFilter f, DateTime newCutoff)
    {
        if (!string.IsNullOrWhiteSpace(f.Q))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{f.Q}%"));
        if (f.CategoryId is not null)
            query = query.Where(p => p.SubCategory.CategoryId == f.CategoryId);
        if (f.SubCategoryId is not null)
            query = query.Where(p => p.SubCategoryId == f.SubCategoryId);
        if (f.BrandIds is { Count: > 0 })
            query = query.Where(p => f.BrandIds.Contains(p.BrandId));
        if (f.TagIds is { Count: > 0 })
            query = query.Where(p => p.ProductTags.Any(pt => f.TagIds.Contains(pt.TagId)));
        if (f.ColorIds is { Count: > 0 })
            query = query.Where(p => p.Variants.Any(v =>
                v.OptionValues.Any(ov => ov.ProductOptionValue.ColorId != null && f.ColorIds.Contains(ov.ProductOptionValue.ColorId!.Value))));
        if (f.MinPrice is not null)
            query = query.Where(p => p.Variants.Any(v => v.IsActive &&
                (v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price) >= f.MinPrice));
        if (f.MaxPrice is not null)
            query = query.Where(p => p.Variants.Any(v => v.IsActive &&
                (v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price) <= f.MaxPrice));
        if (f.Condition is not null)
            query = query.Where(p => p.Condition == f.Condition);
        if (f.MinRating is not null)
            query = query.Where(p => (p.Reviews.Average(r => (double?)r.Rating) ?? 0) >= f.MinRating);
        if (f.HasDiscount == true)
            query = query.Where(p => p.Variants.Any(v => v.IsActive && v.HasDiscount));
        if (f.IsNew == true)
            query = query.Where(p => p.CreatedAt >= newCutoff);
        if (f.InStock == true)
            query = query.Where(p => p.Variants.Any(v => v.IsActive && v.StockCount > 0));
        return query;
    }

    private IQueryable<Product> ApplySort(IQueryable<Product> query, string? sort) => sort switch
    {
        "price_asc" => query.OrderBy(p => p.Variants.Where(v => v.IsActive)
            .Min(v => (decimal?)(v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price))),
        "price_desc" => query.OrderByDescending(p => p.Variants.Where(v => v.IsActive)
            .Min(v => (decimal?)(v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price))),
        "rating" => query.OrderByDescending(p => p.Reviews.Average(r => (double?)r.Rating) ?? 0),
        "popularity" => query.OrderByDescending(p => _db.OrderItems.Where(oi => oi.ProductId == p.Id).Sum(oi => (int?)oi.Quantity) ?? 0),
        _ => query.OrderByDescending(p => p.CreatedAt) // newest (default)
    };

    private static IQueryable<ProductListItemDto> ProjectCards(IQueryable<Product> query, DateTime newCutoff) =>
        query.Select(p => new ProductListItemDto
        {
            Id = p.Id,
            Name = p.Name,
            Code = p.Code,
            BrandName = p.Brand.Name,
            CategoryName = p.SubCategory.Category.Name,
            SubCategoryName = p.SubCategory.Name,
            PrimaryImageUrl = p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                .Select(i => i.Url).FirstOrDefault(),
            FromPrice = p.Variants.Where(v => v.IsActive)
                .Min(v => (decimal?)(v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price)),
            MaxPrice = p.Variants.Where(v => v.IsActive)
                .Max(v => (decimal?)(v.HasDiscount && v.DiscountPrice != null ? v.DiscountPrice!.Value : v.Price)),
            HasDiscount = p.Variants.Any(v => v.IsActive && v.HasDiscount),
            InStock = p.Variants.Any(v => v.IsActive && v.StockCount > 0),
            Condition = p.Condition,
            IsNew = p.CreatedAt >= newCutoff,
            AvgRating = p.Reviews.Average(r => (double?)r.Rating) ?? 0,
            ReviewCount = p.Reviews.Count()
            // Swatches stitched after materialization.
        });

    private async Task<List<ProductListItemDto>> StitchSwatchesAsync(List<ProductListItemDto> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var ids = items.Select(i => i.Id).ToList();
        var rows = await _db.ProductVariantOptionValues.AsNoTracking()
            .Where(link => ids.Contains(link.ProductVariant.ProductId)
                           && link.ProductVariant.IsActive
                           && link.ProductOptionValue.ColorId != null)
            .Select(link => new
            {
                ProductId = link.ProductVariant.ProductId,
                link.ProductOptionValue.Value,
                Hex = link.ProductOptionValue.Color!.HexCode
            })
            .Distinct()
            .ToListAsync(ct);

        var byProduct = rows.GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ColorSwatchDto>)g
                .Select(x => new ColorSwatchDto(x.Value, x.Hex)).ToList());

        return items
            .Select(i => byProduct.TryGetValue(i.Id, out var sw) ? i with { Swatches = sw } : i)
            .ToList();
    }
}
