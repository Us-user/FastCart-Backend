using FastCart.Api.Common;
using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;
using FastCart.Application.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Reviews nested under a product (§6.6): list + create.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/products/{productId:int}/reviews")]
public sealed class ProductReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;
    public ProductReviewsController(IReviewService reviews) => _reviews = reviews;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(int productId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<ProductReviewsResponse>.Ok(await _reviews.ListAsync(productId, pageNumber, pageSize, ct)));

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(int productId, [FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw new UnauthorizedException();
        var result = await _reviews.CreateAsync(productId, userId, request, ct);
        return Ok(ApiResponse<ReviewDto>.Ok(result, "Review posted."));
    }
}

/// <summary>Review deletion (§6.6): admin or the review owner.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;
    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw new UnauthorizedException();
        await _reviews.DeleteAsync(id, userId, User.IsAdmin(), ct);
        return Ok(ApiResponse.Ok("Review deleted."));
    }
}
