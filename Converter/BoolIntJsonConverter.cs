using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRMBusiness.Converter;

public sealed class BoolIntJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.TryGetInt64(out var n) && n != 0,
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => false,
            _ => throw new JsonException($"Token inválido para bool: {reader.TokenType}")
        };
    }

    private static bool ParseString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;

        if (long.TryParse(s, out var n)) return n != 0;
        if (bool.TryParse(s, out var b)) return b;

        return false;
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}
