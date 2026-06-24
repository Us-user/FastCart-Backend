using System.ComponentModel.DataAnnotations;

namespace FastCart.Application.Profile;

/// <summary>Profile read/update (§6.2). Implemented in Infrastructure.</summary>
public interface IProfileService
{
    Task<ProfileDto> GetAsync(string userId, CancellationToken ct = default);
    Task<ProfileDto> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default);
}

/// <summary>Profile projection (§6.2). Email/phone come from the Identity user.</summary>
public sealed record ProfileDto(
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    DateOnly? Dob,
    string? ImageUrl);

/// <summary>
/// Profile update (§6.2, multipart). The optional image arrives as a stream so the
/// Application layer stays free of ASP.NET <c>IFormFile</c>; the controller maps it.
/// </summary>
public sealed record UpdateProfileRequest
{
    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    [Phone]
    public string? PhoneNumber { get; init; }

    public DateOnly? Dob { get; init; }

    public Stream? ImageContent { get; init; }
    public string? ImageFileName { get; init; }
    public string? ImageContentType { get; init; }
}
