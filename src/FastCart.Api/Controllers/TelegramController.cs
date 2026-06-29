using FastCart.Application.Common;
using FastCart.Application.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FastCart.Api.Controllers;

/// <summary>
/// Telegram account-linking (§4.4). Base route: <c>/api/v1/telegram</c>. <c>link/start</c> is
/// for the signed-in user; <c>link/complete</c> is called by the bot with the shared secret.
/// </summary>
[EnableRateLimiting("auth")]
public sealed class TelegramController : BaseApiController
{
    /// <summary>Header carrying the bot ↔ backend shared secret on <c>link/complete</c>.</summary>
    public const string SecretHeader = "X-Telegram-Secret";

    private readonly ITelegramLinkService _link;

    public TelegramController(ITelegramLinkService link) => _link = link;

    [Authorize]
    [HttpPost("link/start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var result = await _link.StartAsync(CurrentUserIdRequired, ct);
        return Ok(ApiResponse<TelegramLinkStartResponse>.Ok(result, "Open the link to connect your Telegram."));
    }

    [AllowAnonymous]
    [HttpPost("link/complete")]
    public async Task<IActionResult> Complete([FromBody] TelegramLinkCompleteRequest request, CancellationToken ct)
    {
        var secret = Request.Headers[SecretHeader].ToString();
        var result = await _link.CompleteAsync(request, secret, ct);
        return Ok(ApiResponse<TelegramLinkCompleteResponse>.Ok(result, "Telegram linked."));
    }
}
