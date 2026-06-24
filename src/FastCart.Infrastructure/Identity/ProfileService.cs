using FastCart.Application.Common.Exceptions;
using FastCart.Application.Common.Interfaces;
using FastCart.Application.Profile;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Identity;

/// <summary>Profile read/update over Identity + the UserProfile table (§6.2).</summary>
public sealed class ProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;

    public ProfileService(UserManager<ApplicationUser> userManager, AppDbContext db, IStorageService storage)
    {
        _userManager = userManager;
        _db = db;
        _storage = storage;
    }

    public async Task<ProfileDto> GetAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        var profile = await _db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        return new ProfileDto(
            profile?.FirstName,
            profile?.LastName,
            user.Email,
            user.PhoneNumber,
            profile?.Dob,
            profile?.ImageUrl);
    }

    public async Task<ProfileDto> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await _userManager.SetEmailAsync(user, request.Email);
            if (!setEmail.Succeeded)
            {
                throw new ValidationException(ToErrors(setEmail));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.PhoneNumber != user.PhoneNumber)
        {
            var setPhone = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
            if (!setPhone.Succeeded)
            {
                throw new ValidationException(ToErrors(setPhone));
            }
        }

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId, FirstName = string.Empty, LastName = string.Empty };
            _db.UserProfiles.Add(profile);
        }

        if (request.FirstName is not null) profile.FirstName = request.FirstName;
        if (request.LastName is not null) profile.LastName = request.LastName;
        if (request.Dob is not null) profile.Dob = request.Dob;

        if (request.ImageContent is not null)
        {
            profile.ImageUrl = await _storage.SaveAsync(
                request.ImageContent,
                request.ImageFileName ?? "avatar",
                request.ImageContentType ?? "application/octet-stream",
                "avatars",
                ct);
        }

        await _db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    private static IReadOnlyDictionary<string, string[]> ToErrors(IdentityResult result) =>
        new Dictionary<string, string[]>
        {
            ["identity"] = result.Errors.Select(e => e.Description).ToArray()
        };
}
