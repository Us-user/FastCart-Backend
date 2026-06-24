using FastCart.Application.Common;
using FastCart.Application.Common.Exceptions;

namespace FastCart.Api.Middleware;

/// <summary>
/// Global exception handler (§4.3): maps thrown exceptions to the standard
/// response envelope with the right status code (400/401/403/404/409/422, else 500).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning(ex, "Handled application exception ({StatusCode}): {Message}", ex.StatusCode, ex.Message);
            await WriteAsync(context, ex.StatusCode, ex.Message, ex.Errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, 500, "An unexpected error occurred.", null);
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string message, object? errors)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(message, errors));
    }
}
