namespace FastCart.Application.Common;

/// <summary>
/// UTC normalization for query-string dates. Unlike JSON bodies (handled by a converter at the
/// API edge), <c>[FromQuery] DateTime?</c> values are model-bound as <see cref="DateTimeKind.Unspecified"/>
/// and must be pinned to UTC before they reach Npgsql's <c>timestamptz</c> columns (§8).
/// </summary>
public static class DateTimeExtensions
{
    public static DateTime? ToUtc(this DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        { } v => v.ToUniversalTime()
    };
}
