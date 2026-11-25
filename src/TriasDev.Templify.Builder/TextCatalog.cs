// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Resources;

namespace TriasDev.Templify.Builder;

/// <summary>
/// The literal texts of templates in several languages: one definition, a text per key and language. Missing
/// translations are build errors, never silent fallbacks.
/// </summary>
public interface ITextCatalog
{
    /// <summary>Looks up a text.</summary>
    /// <param name="key">The text's key.</param>
    /// <param name="language">The document language.</param>
    /// <param name="text">The text, when found.</param>
    /// <returns><see langword="true"/> when the catalog has the key for the language.</returns>
    bool TryGet(string key, CultureInfo language, out string text);
}

/// <summary>Creates and combines <see cref="ITextCatalog"/>s.</summary>
/// <remarks>
/// Lookups go from the specific to the neutral culture (<c>de-AT</c>, then <c>de</c>) but never to the invariant
/// culture: a missing German text must fail the build, not print the default language's text.
/// </remarks>
public static class TextCatalog
{
    /// <summary>Gets a catalog without texts.</summary>
    public static ITextCatalog Empty { get; } = new DictionaryCatalog(new Dictionary<string, IReadOnlyDictionary<string, string>>());

    /// <summary>
    /// A catalog from a dictionary of key → (language → text), for example
    /// <c>["invoiceTitle"] = { ["en"] = "Invoice", ["de"] = "Rechnung" }</c>. Language names compare case-insensitively.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <returns>The catalog.</returns>
    public static ITextCatalog FromDictionary(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return new DictionaryCatalog(texts);
    }

    /// <summary>A catalog over .resx resources.</summary>
    /// <param name="resources">The resource manager.</param>
    /// <param name="neutralLanguage">
    /// The language of the neutral (default) resources, for example English when <c>Texts.resx</c> is English and
    /// <c>Texts.de.resx</c> German. The neutral resources answer only for this language (and its specific cultures);
    /// null to never use them.
    /// </param>
    /// <returns>The catalog.</returns>
    public static ITextCatalog FromResourceManager(ResourceManager resources, CultureInfo? neutralLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new ResourceCatalog(resources, neutralLanguage);
    }

    /// <summary>Combines catalogs; a later catalog overrides the texts of earlier ones (a base plus overrides).</summary>
    /// <param name="catalogs">The catalogs, in order of increasing priority.</param>
    /// <returns>The combined catalog.</returns>
    public static ITextCatalog Combine(params ITextCatalog[] catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        if (catalogs.Any(c => c is null))
        {
            throw new ArgumentException("A catalog is null.", nameof(catalogs));
        }

        return new CombinedCatalog(catalogs.ToArray());
    }

    /// <summary>The culture and its parents, specific to neutral, without the invariant culture.</summary>
    internal static IEnumerable<CultureInfo> Chain(CultureInfo language)
    {
        for (CultureInfo culture = language; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            yield return culture;
        }
    }

    private sealed class DictionaryCatalog(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> texts) : ITextCatalog
    {
        public bool TryGet(string key, CultureInfo language, out string text)
        {
            text = string.Empty;
            if (!texts.TryGetValue(key, out IReadOnlyDictionary<string, string>? translations) || translations is null)
            {
                return false;
            }

            foreach (CultureInfo culture in Chain(language))
            {
                foreach (KeyValuePair<string, string> translation in translations)
                {
                    if (string.Equals(translation.Key, culture.Name, StringComparison.OrdinalIgnoreCase) && translation.Value is not null)
                    {
                        text = translation.Value;
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private sealed class ResourceCatalog(ResourceManager resources, CultureInfo? neutralLanguage) : ITextCatalog
    {
        public bool TryGet(string key, CultureInfo language, out string text)
        {
            text = string.Empty;
            List<CultureInfo> chain = Chain(language).ToList();
            foreach (CultureInfo culture in chain)
            {
                if (Find(culture, key) is { } found)
                {
                    text = found;
                    return true;
                }
            }

            if (neutralLanguage != null && chain.Any(c => string.Equals(c.Name, neutralLanguage.Name, StringComparison.OrdinalIgnoreCase))
                && Find(CultureInfo.InvariantCulture, key) is { } neutral)
            {
                text = neutral;
                return true;
            }

            return false;
        }

        private string? Find(CultureInfo culture, string key)
        {
            try
            {
                return resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)?.GetString(key);
            }
            catch (MissingManifestResourceException)
            {
                return null;
            }
        }
    }

    private sealed class CombinedCatalog(ITextCatalog[] catalogs) : ITextCatalog
    {
        public bool TryGet(string key, CultureInfo language, out string text)
        {
            for (int i = catalogs.Length - 1; i >= 0; i--)
            {
                if (catalogs[i].TryGet(key, language, out text))
                {
                    return true;
                }
            }

            text = string.Empty;
            return false;
        }
    }
}

/// <summary>
/// The texts of the build's language, from <see cref="TemplateBuildOptions.Texts"/>: <c>template.Texts["invoiceTitle"]</c>.
/// </summary>
public sealed class TemplateTexts
{
    private readonly BuildContext _context;
    private readonly ITextCatalog _catalog;
    private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);

    internal TemplateTexts(BuildContext context, ITextCatalog catalog, CultureInfo language)
    {
        _context = context;
        _catalog = catalog;
        Language = language;
    }

    /// <summary>Gets the document language of the build.</summary>
    public CultureInfo Language { get; }

    /// <summary>
    /// Gets the text of a key in the build's language. A missing text is reported when the build ends, together
    /// with every other missing key; until then a marker stands in for it.
    /// </summary>
    /// <param name="key">The text's key.</param>
    public string this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            if (_catalog.TryGet(key, Language, out string text))
            {
                return text;
            }

            if (_reported.Add(key))
            {
                _context.Error(
                    DiagnosticCodes.MissingText,
                    $"The text '{key}' has no translation for {Language.Name} ({Language.EnglishName}).");
            }

            return "[" + key + "]";
        }
    }
}
