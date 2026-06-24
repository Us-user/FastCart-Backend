using System.ComponentModel.DataAnnotations;

namespace FastCart.Application.Addresses;

/// <summary>Address-book operations (§6.3). All scoped to the calling user.</summary>
public interface IAddressService
{
    Task<IReadOnlyList<AddressDto>> ListAsync(string userId, CancellationToken ct = default);
    Task<AddressDto> CreateAsync(string userId, AddressRequest request, CancellationToken ct = default);
    Task<AddressDto> UpdateAsync(string userId, int id, AddressRequest request, CancellationToken ct = default);
    Task DeleteAsync(string userId, int id, CancellationToken ct = default);
    Task<AddressDto> SetDefaultAsync(string userId, int id, CancellationToken ct = default);
}

public sealed record AddressDto(
    int Id,
    string FirstName,
    string LastName,
    string StreetAddress,
    string? Apartment,
    string City,
    string PhoneNumber,
    string Email,
    bool IsDefault);

public sealed record AddressRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; init; } = default!;

    [Required, StringLength(100)]
    public string LastName { get; init; } = default!;

    [Required, StringLength(256)]
    public string StreetAddress { get; init; } = default!;

    [StringLength(100)]
    public string? Apartment { get; init; }

    [Required, StringLength(100)]
    public string City { get; init; } = default!;

    [Required, Phone]
    public string PhoneNumber { get; init; } = default!;

    [Required, EmailAddress]
    public string Email { get; init; } = default!;

    public bool IsDefault { get; init; }
}
