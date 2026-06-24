namespace FastCart.Application.Common.Exceptions;

/// <summary>
/// Base type for application exceptions that map to a specific HTTP status (§4.3).
/// The global exception handler turns these into the standard response envelope.
/// </summary>
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }

    /// <summary>Optional field-level messages for validation failures.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    protected AppException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors;
    }
}

/// <summary>400 — request validation failed.</summary>
public sealed class ValidationException : AppException
{
    public override int StatusCode => 400;

    public ValidationException(IReadOnlyDictionary<string, string[]> errors, string message = "Validation failed.")
        : base(message, errors) { }
}

/// <summary>401 — not authenticated.</summary>
public sealed class UnauthorizedException : AppException
{
    public override int StatusCode => 401;
    public UnauthorizedException(string message = "Authentication is required.") : base(message) { }
}

/// <summary>403 — authenticated but not allowed.</summary>
public sealed class ForbiddenException : AppException
{
    public override int StatusCode => 403;
    public ForbiddenException(string message = "You do not have access to this resource.") : base(message) { }
}

/// <summary>404 — resource not found.</summary>
public sealed class NotFoundException : AppException
{
    public override int StatusCode => 404;
    public NotFoundException(string message = "Resource not found.") : base(message) { }
}

/// <summary>409 — conflict (e.g. out of stock, duplicate SKU).</summary>
public sealed class ConflictException : AppException
{
    public override int StatusCode => 409;
    public ConflictException(string message) : base(message) { }
}

/// <summary>422 — business-rule failure.</summary>
public sealed class BusinessRuleException : AppException
{
    public override int StatusCode => 422;
    public BusinessRuleException(string message) : base(message) { }
}
