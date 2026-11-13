using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaftechCrm.Application.DTOs;

/// <summary>One configurable value shown on the Admin Configuration page.</summary>
public record SystemSettingDto(
    string Key,
    string Value,
    string Category,
    string Label,
    string Description,
    string ValueType, // "int" | "bool" | "string" — tells the frontend which input control to render
    DateTimeOffset? UpdatedAt,
    string? UpdatedByName
);

public record UpdateSystemSettingRequest(
    string Key,
    [property: JsonConverter(typeof(LenientStringJsonConverter))] string Value);

/// <summary>
/// Accepts a JSON number or boolean where a string is expected. The
/// Configuration page renders numeric inputs, so a browser can legitimately
/// send { "value": 5 }; without this the request failed model binding and the
/// page showed a generic error instead of saving.
/// </summary>
public class LenientStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException("Setting value must be a string, number or boolean."),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

/// <summary>Batch update — the Configuration page saves a whole section at once.</summary>
public record UpdateSystemSettingsRequest(List<UpdateSystemSettingRequest> Settings);
