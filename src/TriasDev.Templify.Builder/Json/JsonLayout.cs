// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// Lays out JSON for reading: an object or array whose one-line form fits in the width (counting its indentation and
/// its key) is written on one line, otherwise one member per line, recursively. Strings are never wrapped, and values
/// keep the escaping of the input.
/// </summary>
internal static class JsonLayout
{
    /// <summary>The default line width.</summary>
    public const int DefaultWidth = 100;

    private const string Indent = "  ";

    /// <summary>Formats valid JSON; the result ends with a line break.</summary>
    public static string Format(string json, int width = DefaultWidth)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument document = JsonDocument.Parse(json);
        StringBuilder text = new StringBuilder();
        Write(text, document.RootElement, 0, 0, width);
        return text.Append('\n').ToString();
    }

    private static void Write(StringBuilder text, JsonElement element, int depth, int prefix, int width)
    {
        string single = SingleLine(element);
        if (element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array) || prefix + single.Length <= width || IsEmpty(element))
        {
            text.Append(single);
            return;
        }

        string inner = string.Concat(Enumerable.Repeat(Indent, depth + 1));
        bool isObject = element.ValueKind == JsonValueKind.Object;
        text.Append(isObject ? '{' : '[').Append('\n');
        bool first = true;
        if (isObject)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Separator(text, ref first);
                string key = Key(property.Name) + ": ";
                text.Append(inner).Append(key);
                Write(text, property.Value, depth + 1, inner.Length + key.Length, width);
            }
        }
        else
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                Separator(text, ref first);
                text.Append(inner);
                Write(text, item, depth + 1, inner.Length, width);
            }
        }

        text.Append('\n').Append(string.Concat(Enumerable.Repeat(Indent, depth))).Append(isObject ? '}' : ']');
    }

    private static void Separator(StringBuilder text, ref bool first)
    {
        if (!first)
        {
            text.Append(",\n");
        }

        first = false;
    }

    private static bool IsEmpty(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? !element.EnumerateObject().Any() : element.GetArrayLength() == 0;

    private static string SingleLine(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(", ", element.EnumerateObject().Select(p => Key(p.Name) + ": " + SingleLine(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(", ", element.EnumerateArray().Select(SingleLine)) + "]",
        _ => element.GetRawText(),
    };

    /// <summary>A property name, escaped like the writer escapes it.</summary>
    private static string Key(string name) => "\"" + JsonEncodedText.Encode(name, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\"";
}
