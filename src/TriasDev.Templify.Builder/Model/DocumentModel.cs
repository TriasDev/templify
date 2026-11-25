// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace TriasDev.Templify.Builder.Model;

/// <summary>The format-neutral description of a template that the writers turn into a package.</summary>
/// <param name="Body">The blocks of the document body, in order.</param>
/// <param name="Language">The document language.</param>
internal sealed record DocumentModel(IReadOnlyList<Block> Body, CultureInfo Language);

/// <summary>A block-level element of the document.</summary>
internal abstract record Block;

/// <summary>A paragraph.</summary>
/// <param name="Inlines">The paragraph content, in order.</param>
internal sealed record ParagraphBlock(IReadOnlyList<Inline> Inlines) : Block;

/// <summary>Content inside a paragraph.</summary>
internal abstract record Inline;

/// <summary>Literal text. Tabs and line breaks in it are written as the format's tab and line break.</summary>
/// <param name="Text">The text.</param>
internal sealed record TextInline(string Text) : Inline;

/// <summary>A placeholder for a value from the data.</summary>
/// <param name="Path">The data path; never empty.</param>
internal sealed record ValueInline(TemplatePath Path) : Inline;
