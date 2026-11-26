// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// The <c>texts</c> section of a JSON definition: <c>{"de": {"title": "…"}, "en": {…}}</c>. Looked up like
/// <see cref="TextCatalog.FromDictionary"/> (<c>de-DE</c> finds <c>de</c>).
/// </summary>
internal sealed class EmbeddedTexts
{
    private EmbeddedTexts(IReadOnlyList<TextSection> sections, ITextCatalog catalog)
    {
        Sections = sections;
        Catalog = catalog;
    }

    /// <summary>Gets an empty section.</summary>
    public static EmbeddedTexts None { get; } = new EmbeddedTexts([], TextCatalog.Empty);

    /// <summary>Gets the languages and their texts exactly as in the file (re-exported unchanged).</summary>
    public IReadOnlyList<TextSection> Sections { get; }

    /// <summary>Gets the texts as a catalog.</summary>
    public ITextCatalog Catalog { get; }

    /// <summary>Reads the section of a definition; malformed parts are reported to <paramref name="fail"/> and skipped.</summary>
    public static EmbeddedTexts Read(JsonElement root, Action<string, string>? fail = null)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("texts", out JsonElement texts))
        {
            return None;
        }

        if (texts.ValueKind != JsonValueKind.Object)
        {
            fail?.Invoke("/texts", "The texts are an object of languages, for example { \"de\": { \"title\": \"…\" } }.");
            return None;
        }

        List<TextSection> sections = new List<TextSection>();
        Dictionary<string, Dictionary<string, string>> byKey = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (JsonProperty language in texts.EnumerateObject())
        {
            string at = "/texts/" + JsonComposition.Escape(language.Name);
            if (language.Value.ValueKind != JsonValueKind.Object)
            {
                fail?.Invoke(at, "A language's texts are an object of keys and texts, for example { \"title\": \"…\" }.");
                continue;
            }

            List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();
            sections.Add(new TextSection(language.Name, entries));
            foreach (JsonProperty text in language.Value.EnumerateObject())
            {
                if (text.Value.ValueKind != JsonValueKind.String)
                {
                    fail?.Invoke(at + "/" + JsonComposition.Escape(text.Name), "A text is a string.");
                    continue;
                }

                if (!byKey.TryGetValue(text.Name, out Dictionary<string, string>? translations))
                {
                    byKey[text.Name] = translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                translations[language.Name] = text.Value.GetString()!;
                entries.Add(new KeyValuePair<string, string>(text.Name, text.Value.GetString()!));
            }
        }

        return new EmbeddedTexts(
            sections,
            TextCatalog.FromDictionary(byKey.ToDictionary(e => e.Key, e => (IReadOnlyDictionary<string, string>)e.Value, StringComparer.Ordinal)));
    }
}
