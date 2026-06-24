using FastCart.Application.Addresses;
using FastCart.Application.Common.Exceptions;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Identity;

/// <summary>Address-book CRUD scoped to the calling user (§6.3).</summary>
public sealed class AddressService : IAddressService
{
    private readonly AppDbContext _db;

    public AddressService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AddressDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        return await _db.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AddressDto(
                a.Id, a.FirstName, a.LastName, a.StreetAddress, a.Apartment, a.City, a.PhoneNumber, a.Email, a.IsDefault))
            .ToListAsync(ct);
    }

    public async Task<AddressDto> CreateAsync(string userId, AddressRequest request, CancellationToken ct = default)
    {
        var isFirst = !await _db.Addresses.AnyAsync(a => a.UserId == userId, ct);

        var address = new Address
        {
            UserId = userId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            StreetAddress = request.StreetAddress,
            Apartment = request.Apartment,
            City = request.City,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            IsDefault = request.IsDefault || isFirst
        };

        _db.Addresses.Add(address);

        if (address.IsDefault)
        {
            await UnsetOtherDefaultsAsync(userId, excludeId: null, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Map(address);
    }

    public async Task<AddressDto> UpdateAsync(string userId, int id, AddressRequest request, CancellationToken ct = default)
    {
        var address = await GetOwnedAsync(userId, id, ct);

        address.FirstName = request.FirstName;
        address.LastName = request.LastName;
        address.StreetAddress = request.StreetAddress;
        address.Apartment = request.Apartment;
        address.City = request.City;
        address.PhoneNumber = request.PhoneNumber;
        address.Email = request.Email;

        if (request.IsDefault && !address.IsDefault)
        {
            await UnsetOtherDefaultsAsync(userId, excludeId: id, ct);
            address.IsDefault = true;
        }

        await _db.SaveChangesAsync(ct);
        return Map(address);
    }

    public async Task DeleteAsync(string userId, int id, CancellationToken ct = default)
    {
        var address = await GetOwnedAsync(userId, id, ct);
        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AddressDto> SetDefaultAsync(string userId, int id, CancellationToken ct = default)
    {
        var address = await GetOwnedAsync(userId, id, ct);
        await UnsetOtherDefaultsAsync(userId, excludeId: id, ct);
        address.IsDefault = true;
        await _db.SaveChangesAsync(ct);
        return Map(address);
    }

    private async Task<Address> GetOwnedAsync(string userId, int id, CancellationToken ct)
    {
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        return address ?? throw new NotFoundException("Address not found.");
    }

    private async Task UnsetOtherDefaultsAsync(string userId, int? excludeId, CancellationToken ct)
    {
        var others = await _db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault && (excludeId == null || a.Id != excludeId))
            .ToListAsync(ct);

        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }

    private static AddressDto Map(Address a) => new(
        a.Id, a.FirstName, a.LastName, a.StreetAddress, a.Apartment, a.City, a.PhoneNumber, a.Email, a.IsDefault);
}
