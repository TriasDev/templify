// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// The <c>texts</c> section of a JSON definition: <c>{"title": {"de": "…", "en": "…"}}</c>. Looked up like
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

    /// <summary>Gets the keys and their translations exactly as in the file (re-exported unchanged).</summary>
    public IReadOnlyList<TextSection> Sections { get; }

    /// <summary>Gets the texts as a catalog.</summary>
    public ITextCatalog Catalog { get; }

    /// <summary>
    /// Reads the section of a definition: key first, <c>{"title": {"de": "…", "en": "…"}}</c>. A key may lack a language;
    /// the build then takes it from the host's catalog (TB5001 when that has none either). Malformed parts are reported to
    /// <paramref name="fail"/> and skipped.
    /// </summary>
    public static EmbeddedTexts Read(JsonElement root, Action<string, string>? fail = null)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("texts", out JsonElement texts))
        {
            return None;
        }

        if (texts.ValueKind != JsonValueKind.Object)
        {
            fail?.Invoke("/texts", "The texts are an object of keys, for example { \"title\": { \"de\": \"…\", \"en\": \"…\" } }.");
            return None;
        }

        List<TextSection> entries = new List<TextSection>();
        Dictionary<string, IReadOnlyDictionary<string, string>> byKey = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (JsonProperty key in texts.EnumerateObject())
        {
            string at = "/texts/" + JsonComposition.Escape(key.Name);
            if (key.Value.ValueKind != JsonValueKind.Object)
            {
                fail?.Invoke(at, "A text is an object of languages, for example { \"de\": \"Titel\", \"en\": \"Title\" }.");
                continue;
            }

            List<KeyValuePair<string, string>> translations = new List<KeyValuePair<string, string>>();
            foreach (JsonProperty language in key.Value.EnumerateObject())
            {
                if (language.Value.ValueKind != JsonValueKind.String)
                {
                    fail?.Invoke(at + "/" + JsonComposition.Escape(language.Name), "A translation is a string.");
                    continue;
                }

                translations.Add(new KeyValuePair<string, string>(language.Name, language.Value.GetString()!));
            }

            entries.Add(new TextSection(key.Name, translations));
            byKey[key.Name] = translations.ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
        }

        // Every text has the languages of the others, so an added key cannot miss a language without a word. A regional
        // language is covered by its parent ("de-CH" by "de"), as the build looks it up.
        List<string> languages = entries.SelectMany(e => e.Translations.Select(t => t.Key)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (TextSection entry in entries)
        {
            HashSet<string> own = entry.Translations.Select(t => t.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<string> missing = languages.Where(l => !LanguageChain(l).Any(own.Contains)).ToList();
            if (missing.Count > 0)
            {
                fail?.Invoke(
                    "/texts/" + JsonComposition.Escape(entry.Key),
                    $"The text '{entry.Key}' has no {string.Join(", ", missing.Select(l => $"'{l}'"))} translation; every text has the languages of the others ({string.Join(", ", languages)}).");
            }
        }

        return new EmbeddedTexts(entries, TextCatalog.FromDictionary(byKey));
    }

    /// <summary>A language name and its parents: <c>de-CH</c>, <c>de</c>.</summary>
    private static IEnumerable<string> LanguageChain(string language)
    {
        for (string name = language; name.Length > 0; name = name.LastIndexOf('-') is int dash and > 0 ? name[..dash] : string.Empty)
        {
            yield return name;
        }
    }
}
