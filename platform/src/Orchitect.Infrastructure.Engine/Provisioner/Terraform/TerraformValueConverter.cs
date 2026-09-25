using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

/// <summary>
/// Converts raw score-file parameter values into typed JSON values for a Terraform variable type.
/// </summary>
internal static class TerraformValueConverter
{
    private static readonly string[] ListTypePrefixes = ["list", "set", "tuple"];
    private static readonly string[] MapTypePrefixes = ["map", "object"];

    public static bool TryConvert(string raw, string? variableType, out JsonNode? value, out string error)
    {
        value = null;
        error = string.Empty;
        var type = string.IsNullOrWhiteSpace(variableType) ? null : variableType.Trim();

        switch (type)
        {
            case "string":
                value = JsonValue.Create(raw);
                return true;
            case "number":
                if (TryParseNumber(raw, out var number))
                {
                    value = JsonValue.Create(number);
                    return true;
                }

                error = $"'{raw}' is not a number.";
                return false;
            case "bool":
                if (bool.TryParse(raw, out var boolean))
                {
                    value = JsonValue.Create(boolean);
                    return true;
                }

                error = $"'{raw}' is not a bool.";
                return false;
        }

        if (type is not null && ListTypePrefixes.Any(p => IsTypeOf(type, p)))
        {
            return TryParseCollection<JsonArray>(raw, type, out value, out error);
        }

        if (type is not null && MapTypePrefixes.Any(p => IsTypeOf(type, p)))
        {
            return TryParseCollection<JsonObject>(raw, type, out value, out error);
        }

        value = InferValue(raw);
        return true;
    }

    private static bool IsTypeOf(string type, string prefix) =>
        type == prefix || type.StartsWith($"{prefix}(", StringComparison.Ordinal);

    private static bool TryParseNumber(string raw, out decimal number) =>
        decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

    private static bool TryParseCollection<TNode>(string raw, string type, out JsonNode? value, out string error)
        where TNode : JsonNode
    {
        value = TryParseJson(raw) ?? TryParseJson(raw.Replace('\'', '"'));

        if (value is TNode)
        {
            error = string.Empty;
            return true;
        }

        value = null;
        error = $"'{raw}' is not a valid JSON value for type {type}.";
        return false;
    }

    private static JsonNode? InferValue(string raw)
    {
        if (bool.TryParse(raw, out var boolean))
        {
            return JsonValue.Create(boolean);
        }

        if (TryParseNumber(raw, out var number))
        {
            return JsonValue.Create(number);
        }

        var trimmed = raw.Trim();

        if ((trimmed.StartsWith('[') && trimmed.EndsWith(']')) || (trimmed.StartsWith('{') && trimmed.EndsWith('}')))
        {
            var collection = TryParseJson(trimmed) ?? TryParseJson(trimmed.Replace('\'', '"'));

            if (collection is JsonArray or JsonObject)
            {
                return collection;
            }
        }

        return JsonValue.Create(raw);
    }

    private static JsonNode? TryParseJson(string raw)
    {
        try
        {
            return JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
