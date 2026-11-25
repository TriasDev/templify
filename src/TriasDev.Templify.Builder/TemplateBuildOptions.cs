// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>
/// What one build produces: the file format and the document language.
/// </summary>
public sealed record TemplateBuildOptions
{
    /// <summary>
    /// Gets the file format to write: <see cref="TemplateFormat.Docx"/> or <see cref="TemplateFormat.Odt"/>.
    /// </summary>
    public required TemplateFormat Format { get; init; }

    /// <summary>
    /// Gets the language of the document. It is written into the document properties; it must be a specific
    /// or neutral culture, not the invariant culture.
    /// </summary>
    public required CultureInfo Language { get; init; }
}
