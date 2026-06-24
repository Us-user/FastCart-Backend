using System.ComponentModel.DataAnnotations;
using FastCart.Api.Common;
using FastCart.Application.Common;
using FastCart.Application.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Current user's profile (§6.2). Base route: <c>/api/v1/profile</c>.</summary>
[Authorize]
public sealed class ProfileController : BaseApiController
{
    private readonly IProfileService _profile;

    public ProfileController(IProfileService profile) => _profile = profile;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _profile.GetAsync(CurrentUserIdRequired, ct);
        return Ok(ApiResponse<ProfileDto>.Ok(result));
    }

    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update([FromForm] UpdateProfileForm form, CancellationToken ct)
    {
        if (form.Image is not null) ImageValidation.Validate(form.Image);

        var request = new UpdateProfileRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Email = form.Email,
            PhoneNumber = form.PhoneNumber,
            Dob = form.Dob,
            ImageContent = form.Image?.OpenReadStream(),
            ImageFileName = form.Image?.FileName,
            ImageContentType = form.Image?.ContentType
        };

        var result = await _profile.UpdateAsync(CurrentUserIdRequired, request, ct);
        return Ok(ApiResponse<ProfileDto>.Ok(result, "Profile updated."));
    }
}

/// <summary>Multipart form for profile update (§6.2). Maps to <see cref="UpdateProfileRequest"/>.</summary>
public sealed class UpdateProfileForm
{
    public IFormFile? Image { get; init; }

    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    [Phone]
    public string? PhoneNumber { get; init; }

    public DateOnly? Dob { get; init; }
}
