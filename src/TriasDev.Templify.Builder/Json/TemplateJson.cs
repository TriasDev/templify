// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// Template definitions as JSON: export a definition written in C#, and import a JSON definition as an
/// <see cref="ITemplateDefinition"/> that builds like any other.
/// </summary>
public static class TemplateJson
{
    /// <summary>
    /// Exports a definition: it is composed and checked as for a build (same errors), then written as JSON. Texts from
    /// <see cref="TemplateBuildOptions.Texts"/> are written as <c>[[key]]</c>, so the JSON serves every language; the
    /// language and format of <paramref name="options"/> do not matter.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="options">The options to compose with.</param>
    /// <returns>The JSON and its assets.</returns>
    /// <exception cref="TemplateBuildException">The definition does not build, or a text cannot be written (TB9204).</exception>
    public static TemplateJsonDocument Export(ITemplateDefinition definition, TemplateBuildOptions options) =>
        Export(definition, options, new TemplateJsonExportOptions());

    /// <summary>
    /// Exports a definition; components registered in <paramref name="components"/> are written as references with
    /// their parameters, others as the blocks they produced.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="options">The options to compose with.</param>
    /// <param name="components">The registered components, or null.</param>
    /// <returns>The JSON and its assets.</returns>
    /// <exception cref="TemplateBuildException">The definition does not build, or a text cannot be written (TB9204).</exception>
    public static TemplateJsonDocument Export(ITemplateDefinition definition, TemplateBuildOptions options, ComponentCatalog? components) =>
        Export(definition, options, new TemplateJsonExportOptions { Components = components });

    /// <summary>
    /// Exports a definition with components and, for <see cref="TemplateJsonExportOptions.TextLanguages"/>, the
    /// translations in a <c>texts</c> section (from the definition's own texts if it was imported, then from
    /// <see cref="TemplateBuildOptions.Texts"/>).
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="options">The options to compose with; their catalog supplies the translations.</param>
    /// <param name="export">What to write besides the definition.</param>
    /// <returns>The JSON and its assets.</returns>
    /// <exception cref="TemplateBuildException">The definition does not build, or a text cannot be written (TB9204).</exception>
    public static TemplateJsonDocument Export(ITemplateDefinition definition, TemplateBuildOptions options, TemplateJsonExportOptions export)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(export);
        ComposedTemplate composed = TemplateBuilder.Compose(definition, options, keyMarkers: true);
        EmbeddedTexts own = (definition as JsonDefinition)?.Texts ?? EmbeddedTexts.None;
        ITextCatalog catalog = options.Texts ?? TextCatalog.Empty;
        IEnumerable<string> names = export.TextLanguages is { Count: > 0 } languages ? languages.Select(l => l.Name) : own.Languages;
        List<TextLanguage> texts = names.Select(name =>
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.GetCultureInfo(name);
            return new TextLanguage(name, key =>
                own.Catalog.TryGet(key, culture, out string text) || catalog.TryGet(key, culture, out text) ? text : null);
        }).ToList();
        (string json, IReadOnlyDictionary<string, byte[]> assets) = ModelJsonWriter.Write(composed, definition.Identity!, export.Components, texts);
        return new TemplateJsonDocument(json, assets);
    }

    /// <summary>
    /// Imports a JSON definition. The result builds with <see cref="TemplateBuilder.Build"/> like a C# definition;
    /// problems are reported as build errors with the JSON pointer in <see cref="BuildDiagnostic.Location"/>.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="options">The assets and what the import allows; null for none.</param>
    /// <returns>The definition.</returns>
    /// <exception cref="TemplateBuildException">The text is not a JSON definition (TB9200).</exception>
    public static ITemplateDefinition Import(string json, TemplateJsonImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        options ??= new TemplateJsonImportOptions();
        System.Text.Json.JsonElement root;
        try
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw Invalid(string.Empty, "The text is not JSON: " + exception.Message);
        }

        if (root.ValueKind != System.Text.Json.JsonValueKind.Object
            || !root.TryGetProperty("templifyDefinition", out System.Text.Json.JsonElement version)
            || version.ValueKind != System.Text.Json.JsonValueKind.Number
            || !version.TryGetInt32(out int formatVersion)
            || formatVersion != ModelJsonWriter.FormatVersion)
        {
            throw Invalid("/templifyDefinition", $"A JSON definition starts with \"templifyDefinition\": {ModelJsonWriter.FormatVersion}.");
        }

        if (!root.TryGetProperty("identity", out System.Text.Json.JsonElement identity)
            || identity.ValueKind != System.Text.Json.JsonValueKind.Object
            || !identity.TryGetProperty("id", out System.Text.Json.JsonElement id) || id.ValueKind != System.Text.Json.JsonValueKind.String
            || !identity.TryGetProperty("version", out System.Text.Json.JsonElement number) || !number.TryGetInt32(out int identityVersion))
        {
            throw Invalid("/identity", "The identity { \"id\": …, \"version\": … } is missing or incomplete.");
        }

        JsonDefinition definition = new JsonDefinition(root, new TemplateIdentity(id.GetString()!, identityVersion), options);
        CheckStructure(definition);
        return definition;
    }

    /// <summary>
    /// Composes the definition once without a language, keeping only the problems of the JSON itself (structure,
    /// assets, raw XML, data paths): those are thrown now, together. The composers' rules are checked at build time.
    /// </summary>
    private static void CheckStructure(JsonDefinition definition)
    {
        BuildContext context = new BuildContext();
        TemplateComposer composer = new TemplateComposer(context, new TemplateTexts(context, TextCatalog.Empty, System.Globalization.CultureInfo.InvariantCulture));
        definition.Compose(composer);
        List<BuildDiagnostic> problems = context.Errors
            .Where(e => e.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.RawXmlNotAllowed or DiagnosticCodes.UnknownDataPath)
            .ToList();
        if (problems.Count > 0)
        {
            throw new TemplateBuildException(problems);
        }
    }

    private static TemplateBuildException Invalid(string location, string message) =>
        new TemplateBuildException([new BuildDiagnostic(DiagnosticCodes.InvalidJson, message) { Location = location }]);
}
