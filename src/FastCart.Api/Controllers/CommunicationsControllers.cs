using FastCart.Application.Common;
using FastCart.Application.Communications;
using FastCart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FastCart.Api.Controllers;

// ── Newsletter (§6.13) ───────────────────────────────────────────────────────

/// <summary>Public newsletter subscription (§6.13).</summary>
public sealed class NewsletterController : BaseApiController
{
    private readonly INewsletterService _service;
    public NewsletterController(INewsletterService service) => _service = service;

    [AllowAnonymous]
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] NewsletterSubscribeRequest request, CancellationToken ct)
    {
        await _service.SubscribeAsync(request.Email, ct);
        return Ok(ApiResponse.Ok("Subscribed to the newsletter."));
    }
}

/// <summary>Admin newsletter listing (§6.13). Base route: <c>/api/v1/admin/newsletter</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/newsletter")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminNewsletterController : ControllerBase
{
    private readonly INewsletterService _service;
    public AdminNewsletterController(INewsletterService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<NewsletterSubscriberDto>>.Ok(await _service.ListAsync(pageNumber, pageSize, ct)));
}

// ── Contact (§6.13) ──────────────────────────────────────────────────────────

/// <summary>Public contact-form submission (§6.13).</summary>
public sealed class ContactController : BaseApiController
{
    private readonly IContactService _service;
    public ContactController(IContactService service) => _service = service;

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ContactMessageRequest request, CancellationToken ct)
    {
        await _service.CreateAsync(request, ct);
        return Ok(ApiResponse.Ok("Message sent."));
    }
}

/// <summary>Admin contact-message listing (§6.13). Base route: <c>/api/v1/admin/contact-messages</c>.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/admin/contact-messages")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminContactMessagesController : ControllerBase
{
    private readonly IContactService _service;
    public AdminContactMessagesController(IContactService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<ContactMessageDto>>.Ok(await _service.ListAsync(pageNumber, pageSize, ct)));
}
