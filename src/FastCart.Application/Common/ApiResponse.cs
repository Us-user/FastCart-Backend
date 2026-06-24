namespace FastCart.Application.Common;

/// <summary>
/// Standard response envelope (§4.3): { success, message, data, errors }.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public object? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Message = message, Data = data, Errors = null };

    public static ApiResponse<T> Fail(string message, object? errors = null) =>
        new() { Success = false, Message = message, Data = default, Errors = errors };
}

/// <summary>
/// Non-generic helpers for envelopes without a data payload.
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object?> Ok(string? message = null) =>
        new() { Success = true, Message = message, Data = null, Errors = null };

    public static ApiResponse<object?> Fail(string message, object? errors = null) =>
        new() { Success = false, Message = message, Data = null, Errors = errors };
}
