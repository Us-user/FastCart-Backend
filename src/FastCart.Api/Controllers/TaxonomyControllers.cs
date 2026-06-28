using FastCart.Api.Common;
using FastCart.Application.Catalog;
using FastCart.Application.Common;
using FastCart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Categories (§6.4). Public reads; Admin writes. Create/update are multipart.</summary>
public sealed class CategoriesController : BaseApiController
{
    private readonly ICategoryService _service;
    public CategoriesController(ICategoryService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeSubcategories = false, CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(await _service.ListAsync(includeSubcategories, ct)));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<CategoryDto>.Ok(await _service.GetAsync(id, ct)));

    [Authorize(Roles = Roles.Management)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CategoryForm form, CancellationToken ct)
    {
        if (form.Image is not null) ImageValidation.Validate(form.Image);
        var result = await _service.CreateAsync(form.ToInput(), ct);
        return Ok(ApiResponse<CategoryDto>.Ok(result, "Category created."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(int id, [FromForm] CategoryForm form, CancellationToken ct)
    {
        if (form.Image is not null) ImageValidation.Validate(form.Image);
        var result = await _service.UpdateAsync(id, form.ToInput(), ct);
        return Ok(ApiResponse<CategoryDto>.Ok(result, "Category updated."));
    }

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Category deleted."));
    }
}

/// <summary>Multipart form for category create/update (§6.4).</summary>
public sealed class CategoryForm
{
    public string? Name { get; init; }
    public IFormFile? Image { get; init; }

    public CategoryInput ToInput() => new()
    {
        Name = Name ?? string.Empty,
        ImageContent = Image?.OpenReadStream(),
        ImageFileName = Image?.FileName,
        ImageContentType = Image?.ContentType
    };
}

/// <summary>Subcategories (§6.4).</summary>
public sealed class SubCategoriesController : BaseApiController
{
    private readonly ISubCategoryService _service;
    public SubCategoriesController(ISubCategoryService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? categoryId, CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<SubCategoryDto>>.Ok(await _service.ListAsync(categoryId, ct)));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<SubCategoryDto>.Ok(await _service.GetAsync(id, ct)));

    [Authorize(Roles = Roles.Management)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SubCategoryRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SubCategoryDto>.Ok(await _service.CreateAsync(request, ct), "Subcategory created."));

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SubCategoryRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SubCategoryDto>.Ok(await _service.UpdateAsync(id, request, ct), "Subcategory updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Subcategory deleted."));
    }
}

/// <summary>Brands (§6.4).</summary>
public sealed class BrandsController : BaseApiController
{
    private readonly IBrandService _service;
    public BrandsController(IBrandService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? brandName, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<BrandDto>>.Ok(await _service.ListAsync(brandName, pageNumber, pageSize, ct)));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<BrandDto>.Ok(await _service.GetAsync(id, ct)));

    [Authorize(Roles = Roles.Management)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BrandRequest request, CancellationToken ct) =>
        Ok(ApiResponse<BrandDto>.Ok(await _service.CreateAsync(request, ct), "Brand created."));

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BrandRequest request, CancellationToken ct) =>
        Ok(ApiResponse<BrandDto>.Ok(await _service.UpdateAsync(id, request, ct), "Brand updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Brand deleted."));
    }
}

/// <summary>Colors (§6.4).</summary>
public sealed class ColorsController : BaseApiController
{
    private readonly IColorService _service;
    public ColorsController(IColorService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? colorName, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<ColorDto>>.Ok(await _service.ListAsync(colorName, pageNumber, pageSize, ct)));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<ColorDto>.Ok(await _service.GetAsync(id, ct)));

    [Authorize(Roles = Roles.Management)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ColorRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ColorDto>.Ok(await _service.CreateAsync(request, ct), "Color created."));

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ColorRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ColorDto>.Ok(await _service.UpdateAsync(id, request, ct), "Color updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Color deleted."));
    }
}

/// <summary>Tags (§6.4).</summary>
public sealed class TagsController : BaseApiController
{
    private readonly ITagService _service;
    public TagsController(ITagService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<TagDto>>.Ok(await _service.ListAsync(ct)));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<TagDto>.Ok(await _service.GetAsync(id, ct)));

    [Authorize(Roles = Roles.Management)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TagRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TagDto>.Ok(await _service.CreateAsync(request, ct), "Tag created."));

    [Authorize(Roles = Roles.Management)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TagRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TagDto>.Ok(await _service.UpdateAsync(id, request, ct), "Tag updated."));

    [Authorize(Roles = Roles.Management)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Tag deleted."));
    }
}
