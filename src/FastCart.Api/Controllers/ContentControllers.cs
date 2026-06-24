using FastCart.Api.Common;
using FastCart.Application.Common;
using FastCart.Application.Content;
using FastCart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

// ── Sliders (§6.12) ──────────────────────────────────────────────────────────

/// <summary>Public home-page sliders (§6.12).</summary>
public sealed class SlidersController : BaseApiController
{
    private readonly ISliderService _service;
    public SlidersController(ISliderService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<SliderDto>>.Ok(await _service.ListActiveAsync(ct)));
}

/// <summary>Admin slider management (§6.12). Base route: <c>/api/v1/admin/sliders</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/sliders")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminSlidersController : ControllerBase
{
    private readonly ISliderService _service;
    public AdminSlidersController(ISliderService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<SliderDto>>.Ok(await _service.ListAllAsync(ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<SliderDto>.Ok(await _service.GetAsync(id, ct)));

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] SliderForm form, CancellationToken ct)
    {
        if (form.Image is not null) ImageValidation.Validate(form.Image);
        return Ok(ApiResponse<SliderDto>.Ok(await _service.CreateAsync(form.ToInput(), ct), "Slider created."));
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(int id, [FromForm] SliderForm form, CancellationToken ct)
    {
        if (form.Image is not null) ImageValidation.Validate(form.Image);
        return Ok(ApiResponse<SliderDto>.Ok(await _service.UpdateAsync(id, form.ToInput(), ct), "Slider updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Slider deleted."));
    }
}

/// <summary>Multipart form for slider create/update (§6.12).</summary>
public sealed class SliderForm
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public int SortOrder { get; init; }
    public bool? IsActive { get; init; }
    public IFormFile? Image { get; init; }

    public SliderInput ToInput() => new()
    {
        Title = Title,
        Subtitle = Subtitle,
        SortOrder = SortOrder,
        IsActive = IsActive ?? true,
        ImageContent = Image?.OpenReadStream(),
        ImageFileName = Image?.FileName,
        ImageContentType = Image?.ContentType
    };
}

// ── Banners (§6.12) ──────────────────────────────────────────────────────────

/// <summary>Public promo banners — backs Flash Sales (§6.12).</summary>
public sealed class BannersController : BaseApiController
{
    private readonly IBannerService _service;
    public BannersController(IBannerService service) => _service = service;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<BannerDto>>.Ok(await _service.ListActiveAsync(ct)));
}

/// <summary>Admin banner management (§6.12). Base route: <c>/api/v1/admin/banners</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/banners")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminBannersController : ControllerBase
{
    private readonly IBannerService _service;
    public AdminBannersController(IBannerService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<BannerDto>>.Ok(await _service.ListAllAsync(ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<BannerDto>.Ok(await _service.GetAsync(id, ct)));

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] BannerForm form, CancellationToken ct)
    {
        if (form.Image is not null) ImageValidation.Validate(form.Image);
        return Ok(ApiResponse<BannerDto>.Ok(await _service.CreateAsync(form.ToInput(), ct), "Banner created."));
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(int id, [FromForm] BannerForm form, CancellationToken ct)
    {
        if (form.Image is not null) ImageValidation.Validate(form.Image);
        return Ok(ApiResponse<BannerDto>.Ok(await _service.UpdateAsync(id, form.ToInput(), ct), "Banner updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Banner deleted."));
    }
}

/// <summary>Multipart form for banner create/update (§6.12).</summary>
public sealed class BannerForm
{
    public string? Title { get; init; }
    public int? CategoryId { get; init; }
    public DateTime? EndsAt { get; init; }
    public bool? IsActive { get; init; }
    public IFormFile? Image { get; init; }

    public BannerInput ToInput() => new()
    {
        Title = Title,
        CategoryId = CategoryId,
        EndsAt = EndsAt,
        IsActive = IsActive ?? true,
        ImageContent = Image?.OpenReadStream(),
        ImageFileName = Image?.FileName,
        ImageContentType = Image?.ContentType
    };
}
