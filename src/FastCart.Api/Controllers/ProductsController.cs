using System.Text.Json;
using System.Text.Json.Serialization;
using FastCart.Api.Common;
using FastCart.Application.Catalog;
using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Domain.Common;
using FastCart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Products &amp; variants (§6.5). Base route: <c>/api/v1/products</c>.</summary>
public sealed class ProductsController : BaseApiController
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductFilter filter, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<ProductListItemDto>>.Ok(await _service.ListAsync(filter, ct)));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<ProductDetailDto>.Ok(await _service.GetAsync(id, ct)));

    [AllowAnonymous]
    [HttpGet("{id:int}/related")]
    public async Task<IActionResult> Related(int id, [FromQuery] int take = 8, CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<ProductListItemDto>>.Ok(await _service.GetRelatedAsync(id, take, ct)));

    [Authorize(Roles = Roles.Management)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateProductForm form, CancellationToken ct)
    {
        ImageValidation.ValidateAll(form.Images);

        var request = new CreateProductRequest
        {
            Name = form.Name,
            Code = form.Code,
            Description = form.Description,
            SubCategoryId = form.SubCategoryId,
            BrandId = form.BrandId,
            IsTaxable = form.IsTaxable,
            Condition = form.Condition,
            TagIds = form.TagIds ?? new List<int>(),
            Options = Parse<CreateOptionInput>(form.Options, nameof(form.Options)),
            Variants = Parse<CreateVariantInput>(form.Variants, nameof(form.Variants)),
            Images = (form.Images ?? new List<IFormFile>())
                .Select(f => new ImageUpload(f.OpenReadStream(), f.FileName, f.ContentType)).ToList()
        };

        var result = await _service.CreateAsync(request, ct);
        return Ok(ApiResponse<ProductDetailDto>.Ok(result, "Product created."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ProductDetailDto>.Ok(await _service.UpdateAsync(id, request, ct), "Product updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Product deleted."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request, CancellationToken ct)
    {
        await _service.BulkDeleteAsync(request.Ids, ct);
        return Ok(ApiResponse.Ok("Products deleted."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpPost("{id:int}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddImages(int id, [FromForm] AddImagesForm form, CancellationToken ct)
    {
        ImageValidation.ValidateAll(form.Images);
        var uploads = (form.Images ?? new List<IFormFile>())
            .Select(f => new ImageUpload(f.OpenReadStream(), f.FileName, f.ContentType)).ToList();
        return Ok(ApiResponse<IReadOnlyList<ImageDto>>.Ok(await _service.AddImagesAsync(id, uploads, ct), "Images added."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken ct)
    {
        await _service.DeleteImageAsync(id, imageId, ct);
        return Ok(ApiResponse.Ok("Image removed."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpGet("{id:int}/variants")]
    public async Task<IActionResult> ListVariants(int id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<AdminVariantDto>>.Ok(await _service.ListVariantsAsync(id, ct)));

    [Authorize(Roles = Roles.Management)]
    [HttpPost("{id:int}/variants")]
    public async Task<IActionResult> AddVariant(int id, [FromBody] AddVariantRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AdminVariantDto>.Ok(await _service.AddVariantAsync(id, request, ct), "Variant added."));

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}/variants/{variantId:int}")]
    public async Task<IActionResult> UpdateVariant(int id, int variantId, [FromBody] UpdateVariantRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AdminVariantDto>.Ok(await _service.UpdateVariantAsync(id, variantId, request, ct), "Variant updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}/variants/{variantId:int}")]
    public async Task<IActionResult> DeleteVariant(int id, int variantId, CancellationToken ct)
    {
        await _service.DeleteVariantAsync(id, variantId, ct);
        return Ok(ApiResponse.Ok("Variant deleted."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}/variants/{variantId:int}/stock")]
    public async Task<IActionResult> UpdateStock(int id, int variantId, [FromBody] StockUpdateRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AdminVariantDto>.Ok(await _service.UpdateStockAsync(id, variantId, request.Count, ct), "Stock updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpPost("{id:int}/options")]
    public async Task<IActionResult> AddOption(int id, [FromBody] OptionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OptionDto>.Ok(await _service.AddOptionAsync(id, request, ct), "Option added."));

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}/options/{optionId:int}")]
    public async Task<IActionResult> UpdateOption(int id, int optionId, [FromBody] OptionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OptionDto>.Ok(await _service.UpdateOptionAsync(id, optionId, request, ct), "Option updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}/options/{optionId:int}")]
    public async Task<IActionResult> DeleteOption(int id, int optionId, CancellationToken ct)
    {
        await _service.DeleteOptionAsync(id, optionId, ct);
        return Ok(ApiResponse.Ok("Option deleted."));
    }

    private static List<T> Parse<T>(string? json, string field)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [field] = new[] { $"Invalid JSON: {ex.Message}" }
            });
        }
    }
}

/// <summary>
/// Multipart form for product create (§6.5). Scalars + tag ids + image files, with
/// <c>options</c> and <c>variants</c> supplied as JSON-array strings.
/// </summary>
public sealed class CreateProductForm
{
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public int SubCategoryId { get; init; }
    public int BrandId { get; init; }
    public bool IsTaxable { get; init; }
    public ProductCondition? Condition { get; init; }
    public List<int>? TagIds { get; init; }

    /// <summary>JSON array: [{ name, sortOrder, values: [{ value, colorId?, sortOrder }] }].</summary>
    public string? Options { get; init; }

    /// <summary>JSON array: [{ sku, optionValues: [{ optionName, value }], price, hasDiscount, discountPrice?, costPrice, count, isActive }].</summary>
    public string? Variants { get; init; }

    public List<IFormFile>? Images { get; init; }
}

/// <summary>Bulk-delete payload (§6.5).</summary>
public sealed record BulkDeleteRequest
{
    public List<int> Ids { get; init; } = new();
}

/// <summary>Multipart form for adding product images (§6.5).</summary>
public sealed class AddImagesForm
{
    public List<IFormFile>? Images { get; init; }
}
