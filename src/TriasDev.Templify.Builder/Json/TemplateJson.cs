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
    /// Exports a definition: it is composed and checked as for a build (same errors), then written as JSON. The
    /// texts are those of <paramref name="options"/>' language; the format does not matter.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="options">The language and texts to compose with.</param>
    /// <returns>The JSON and its assets.</returns>
    /// <exception cref="TemplateBuildException">The definition does not build.</exception>
    public static TemplateJsonDocument Export(ITemplateDefinition definition, TemplateBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ComposedTemplate composed = TemplateBuilder.Compose(definition, options);
        (string json, IReadOnlyDictionary<string, byte[]> assets) = ModelJsonWriter.Write(composed, definition.Identity!);
        return new TemplateJsonDocument(json, assets);
    }
}
