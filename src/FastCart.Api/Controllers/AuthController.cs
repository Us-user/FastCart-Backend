using FastCart.Application.Auth;
using FastCart.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FastCart.Api.Controllers;

/// <summary>Authentication &amp; accounts (§6.1). Base route: <c>/api/v1/auth</c>.</summary>
[EnableRateLimiting("auth")]
public sealed class AuthController : BaseApiController
{
    private readonly IAuthService _auth;
    private readonly IPasswordResetService _reset;

    public AuthController(IAuthService auth, IPasswordResetService reset)
    {
        _auth = auth;
        _reset = reset;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        return Ok(ApiResponse<AuthResult>.Ok(result, "Registration successful."));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        return Ok(ApiResponse<AuthResult>.Ok(result, "Login successful."));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(request, ct);
        return Ok(ApiResponse<AuthResult>.Ok(result, "Token refreshed."));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(CurrentUserIdRequired, request, ct);
        return Ok(ApiResponse.Ok("Logged out."));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        // Anti-enumeration: always 200 (§4.4).
        return Ok(ApiResponse.Ok("If the email exists, a reset link has been sent."));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return Ok(ApiResponse.Ok("Password has been reset."));
    }

    // --- Telegram-delivered reset (§4.4). Coexists with the email reset above. ---

    [AllowAnonymous]
    [HttpPost("reset/request")]
    public async Task<IActionResult> ResetRequest([FromBody] TelegramResetRequestRequest request, CancellationToken ct)
    {
        await _reset.RequestAsync(request, ct);
        // Anti-enumeration: always 200, regardless of account existence or Telegram link state.
        return Ok(ApiResponse.Ok("If the account exists and Telegram is linked, a code has been sent."));
    }

    [AllowAnonymous]
    [HttpPost("reset/verify")]
    public async Task<IActionResult> ResetVerify([FromBody] TelegramResetVerifyRequest request, CancellationToken ct)
    {
        var result = await _reset.VerifyAsync(request, ct);
        return Ok(ApiResponse<TelegramResetVerifyResponse>.Ok(result, "Code verified."));
    }

    [AllowAnonymous]
    [HttpPost("reset/confirm")]
    public async Task<IActionResult> ResetConfirm([FromBody] TelegramResetConfirmRequest request, CancellationToken ct)
    {
        await _reset.ConfirmAsync(request, ct);
        return Ok(ApiResponse.Ok("Password has been reset."));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(CurrentUserIdRequired, request, ct);
        return Ok(ApiResponse.Ok("Password changed."));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await _auth.GetMeAsync(CurrentUserIdRequired, ct);
        return Ok(ApiResponse<MeResponse>.Ok(result));
    }
}
