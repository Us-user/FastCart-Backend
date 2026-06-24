using FastCart.Application.Common;
using FastCart.Application.Coupons;
using FastCart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Customer coupon validation (§6.9). Base route: <c>/api/v1/coupons</c>.</summary>
[Authorize]
public sealed class CouponsController : BaseApiController
{
    private readonly ICouponService _coupons;
    public CouponsController(ICouponService coupons) => _coupons = coupons;

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateCouponRequest request, CancellationToken ct) =>
        Ok(ApiResponse<CouponValidationResult>.Ok(await _coupons.ValidateAsync(CurrentUserIdRequired, request, ct)));
}

/// <summary>Admin coupon management (§6.9). Base route: <c>/api/v1/admin/coupons</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/coupons")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminCouponsController : ControllerBase
{
    private readonly ICouponService _coupons;
    public AdminCouponsController(ICouponService coupons) => _coupons = coupons;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<CouponDto>>.Ok(await _coupons.ListAsync(pageNumber, pageSize, ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<CouponDto>.Ok(await _coupons.GetAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CouponRequest request, CancellationToken ct) =>
        Ok(ApiResponse<CouponDto>.Ok(await _coupons.CreateAsync(request, ct), "Coupon created."));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CouponRequest request, CancellationToken ct) =>
        Ok(ApiResponse<CouponDto>.Ok(await _coupons.UpdateAsync(id, request, ct), "Coupon updated."));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _coupons.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Coupon deleted."));
    }
}
