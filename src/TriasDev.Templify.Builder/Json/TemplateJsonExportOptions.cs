// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace TriasDev.Templify.Builder.Json;

/// <summary>What a JSON export writes besides the definition.</summary>
public sealed class TemplateJsonExportOptions
{
    /// <summary>Gets the components written as references with their parameters; others are written as the blocks they produced.</summary>
    public ComponentCatalog? Components { get; init; }

    /// <summary>
    /// Gets the languages whose translations are written into the JSON's <c>texts</c> section, so the file is
    /// self-contained. Null or empty: only the <c>[[key]]</c> references are written (an imported definition keeps the
    /// languages of its own <c>texts</c>); the build then translates from <see cref="TemplateBuildOptions.Texts"/>.
    /// </summary>
    public IReadOnlyList<CultureInfo>? TextLanguages { get; init; }
}
