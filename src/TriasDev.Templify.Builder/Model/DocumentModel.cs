// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace TriasDev.Templify.Builder.Model;

/// <summary>The format-neutral description of a template that the writers turn into a package.</summary>
/// <param name="Body">The blocks of the document body, in order.</param>
/// <param name="Language">The document language.</param>
/// <param name="Styles">The named styles; <see cref="StyleSheet.Empty"/> when the definition sets none.</param>
/// <param name="Info">The standard document properties.</param>
internal sealed record DocumentModel(IReadOnlyList<Block> Body, CultureInfo Language, StyleSheet Styles, DocumentInfo Info);

/// <summary>A block-level element of the document.</summary>
internal abstract record Block;

/// <summary>A paragraph.</summary>
/// <param name="Inlines">The paragraph content, in order.</param>
/// <param name="Style">The paragraph style; null for the format's default paragraph style.</param>
internal sealed record ParagraphBlock(IReadOnlyList<Inline> Inlines, ParagraphStyleKey? Style = null) : Block;

/// <summary>A page break: the content after it starts on a new page.</summary>
internal sealed record PageBreakBlock : Block;

/// <summary>An if / elseif / else chain. Branches and the else part are filled while the chain is composed.</summary>
internal sealed record ConditionalBlock : Block
{
    public List<ConditionalBranch> Branches { get; } = new List<ConditionalBranch>();

    /// <summary>Gets or sets the else part; null when there is none.</summary>
    public List<Block>? Else { get; set; }
}

/// <summary>One condition of a <see cref="ConditionalBlock"/> and the blocks it guards.</summary>
internal sealed record ConditionalBranch(Condition Condition, List<Block> Body);

/// <summary>A marker paragraph, produced from a <see cref="ConditionalBlock"/> by <see cref="BlockFlattener"/>.</summary>
/// <param name="Text">The marker, for example <c>{{#if order.isPaid}}</c>.</param>
internal sealed record MarkerBlock(string Text) : Block;

/// <summary>Turns conditional blocks into marker paragraphs around their content, in document order.</summary>
internal static class BlockFlattener
{
    public static IEnumerable<Block> Flatten(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            if (block is not ConditionalBlock conditional)
            {
                yield return block;
                continue;
            }

            for (int i = 0; i < conditional.Branches.Count; i++)
            {
                ConditionalBranch branch = conditional.Branches[i];
                yield return new MarkerBlock(i == 0 ? TemplateSyntax.If(branch.Condition) : TemplateSyntax.ElseIf(branch.Condition));
                foreach (Block inner in Flatten(branch.Body))
                {
                    yield return inner;
                }
            }

            if (conditional.Else is { } otherwise)
            {
                yield return new MarkerBlock(TemplateSyntax.Else);
                foreach (Block inner in Flatten(otherwise))
                {
                    yield return inner;
                }
            }

            yield return new MarkerBlock(TemplateSyntax.EndIf);
        }
    }

    /// <summary>Every paragraph, also inside conditional blocks.</summary>
    public static IEnumerable<ParagraphBlock> Paragraphs(IEnumerable<Block> blocks) => Flatten(blocks).OfType<ParagraphBlock>();

    /// <summary>Whether the blocks contain a conditional block at any depth.</summary>
    public static bool HasMarkers(IEnumerable<Block> blocks) => Flatten(blocks).OfType<MarkerBlock>().Any();
}

/// <summary>Content inside a paragraph.</summary>
/// <param name="Style">The character style; null for the paragraph's own formatting.</param>
internal abstract record Inline(CharacterStyleKey? Style);

/// <summary>Literal text. Tabs and line breaks in it are written as the format's tab and line break.</summary>
/// <param name="Text">The text.</param>
/// <param name="Style">The character style, or null.</param>
internal sealed record TextInline(string Text, CharacterStyleKey? Style = null) : Inline(Style);

/// <summary>A placeholder for a value from the data.</summary>
/// <param name="Path">The data path; never empty.</param>
/// <param name="Format">The format specifier, or null.</param>
/// <param name="Style">The character style, or null.</param>
internal sealed record ValueInline(TemplatePath Path, ValueFormat? Format = null, CharacterStyleKey? Style = null) : Inline(Style);
