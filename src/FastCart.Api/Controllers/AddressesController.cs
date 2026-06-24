using FastCart.Application.Addresses;
using FastCart.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

/// <summary>Address book (§6.3). Base route: <c>/api/v1/addresses</c>. All owner-scoped.</summary>
[Authorize]
public sealed class AddressesController : BaseApiController
{
    private readonly IAddressService _addresses;

    public AddressesController(IAddressService addresses) => _addresses = addresses;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _addresses.ListAsync(CurrentUserIdRequired, ct);
        return Ok(ApiResponse<IReadOnlyList<AddressDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddressRequest request, CancellationToken ct)
    {
        var result = await _addresses.CreateAsync(CurrentUserIdRequired, request, ct);
        return Ok(ApiResponse<AddressDto>.Ok(result, "Address added."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AddressRequest request, CancellationToken ct)
    {
        var result = await _addresses.UpdateAsync(CurrentUserIdRequired, id, request, ct);
        return Ok(ApiResponse<AddressDto>.Ok(result, "Address updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _addresses.DeleteAsync(CurrentUserIdRequired, id, ct);
        return Ok(ApiResponse.Ok("Address deleted."));
    }

    [HttpPut("{id:int}/default")]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        var result = await _addresses.SetDefaultAsync(CurrentUserIdRequired, id, ct);
        return Ok(ApiResponse<AddressDto>.Ok(result, "Default address set."));
    }
}
