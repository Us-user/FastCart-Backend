using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastCart.Api.Common;

/// <summary>
/// Forces every inbound JSON <see cref="DateTime"/> to UTC. PostgreSQL maps our
/// <c>DateTime</c> columns to <c>timestamptz</c>, which Npgsql will only write/compare with
/// <see cref="DateTimeKind.Utc"/>; a client value like <c>"2026-01-01"</c> deserializes as
/// <see cref="DateTimeKind.Unspecified"/> and would otherwise throw at the data layer (§8).
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime());
}

/// <summary>Nullable counterpart to <see cref="UtcDateTimeJsonConverter"/>.</summary>
public sealed class NullableUtcDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var value = reader.GetDateTime();
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        var v = value.Value;
        writer.WriteStringValue(v.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(v, DateTimeKind.Utc)
            : v.ToUniversalTime());
    }
}
