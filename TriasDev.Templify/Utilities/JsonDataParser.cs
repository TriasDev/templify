using System.Text.Json;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Utility class for parsing JSON strings into dictionary data structures for template processing.
/// </summary>
public static class JsonDataParser
{
    /// <summary>
    /// Parses a JSON string into a dictionary that can be used for template processing.
    /// </summary>
    /// <param name="jsonString">The JSON string to parse. Must represent a JSON object (not an array).</param>
    /// <returns>A dictionary containing the parsed JSON data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when jsonString is null.</exception>
    /// <exception cref="ArgumentException">Thrown when jsonString is empty or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when JSON is invalid or root is not an object.</exception>
    public static Dictionary<string, object> ParseJsonToDataDictionary(string jsonString)
    {
        if (jsonString == null)
        {
            throw new ArgumentNullException(nameof(jsonString), "JSON string cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(jsonString))
        {
            throw new ArgumentException("JSON string cannot be empty or whitespace.", nameof(jsonString));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonString);
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Invalid JSON format: {ex.Message}", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(
                    $"JSON root must be an object, but found {document.RootElement.ValueKind}. " +
                    "Arrays and primitive values at the root level are not supported.");
            }

            return ConvertJsonElementToDictionary(document.RootElement);
        }
    }

    /// <summary>
    /// Converts a JsonElement to a Dictionary&lt;string, object&gt;.
    /// </summary>
    private static Dictionary<string, object> ConvertJsonElementToDictionary(JsonElement element)
    {
        Dictionary<string, object> dictionary = new Dictionary<string, object>();

        foreach (JsonProperty property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertJsonElementToObject(property.Value);
        }

        return dictionary;
    }

    /// <summary>
    /// Converts a JsonElement to an appropriate .NET object based on its type.
    /// </summary>
    private static object ConvertJsonElementToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return ConvertJsonElementToDictionary(element);

            case JsonValueKind.Array:
                List<object> list = new List<object>();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElementToObject(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;

            case JsonValueKind.Number:
                // Try to preserve the numeric type
                if (element.TryGetInt32(out int intValue))
                {
                    return intValue;
                }
                if (element.TryGetInt64(out long longValue))
                {
                    return longValue;
                }
                if (element.TryGetDecimal(out decimal decimalValue))
                {
                    return decimalValue;
                }
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null!;

            default:
                throw new JsonException($"Unsupported JSON value kind: {element.ValueKind}");
        }
    }
}
