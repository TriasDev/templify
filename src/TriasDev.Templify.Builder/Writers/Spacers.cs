// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder.Writers;

/// <summary>
/// Spacer paragraphs (style "Template Spacer", 1 pt) keep containers well-formed once Templify removes the marker
/// paragraphs: a table cell must still end with a paragraph — LibreOffice drops the borders and padding of a table whose
/// cell ends with a table — and a table must not touch the next one, which Word and LibreOffice would merge. A writer
/// detail: spacers are not in the model, so definitions, JSON and the data contract do not see them.
/// </summary>
internal static class Spacers
{
    /// <summary>The spacer paragraph.</summary>
    public static ParagraphBlock Paragraph { get; } = new ParagraphBlock([], BuilderStyles.TemplateSpacer);

    /// <summary>After a table (or raw block) that a marker follows: once the marker is gone, the next table would touch it.</summary>
    public static bool After(Block current, Block? next, Func<RawXmlBlock, bool> rawIsParagraph) =>
        (current is TableBlock || (current is RawXmlBlock raw && !rawIsParagraph(raw))) && next is MarkerBlock;

    /// <summary>At the end of a cell whose content may not end with a paragraph of its own once the markers are gone.</summary>
    public static bool AtCellEnd(IReadOnlyList<Block> blocks, Func<RawXmlBlock, bool> rawIsParagraph) => blocks.Count > 0 && !EndsWithParagraph(blocks, rawIsParagraph);

    /// <summary>
    /// At the end of the body, a header or a footer that ends with a marker and may end with a table once it is gone (an
    /// empty part is fine; a table at the end is not).
    /// </summary>
    public static bool AtPartEnd(IReadOnlyList<Block> blocks, Func<RawXmlBlock, bool> rawIsParagraph) =>
        BlockFlattener.Flatten(blocks).LastOrDefault() is MarkerBlock && MayEndWithTable(blocks, rawIsParagraph);

    /// <summary>Whether a table (or a raw table) can be the last element once Templify removed markers and branches.</summary>
    public static bool MayEndWithTable(IReadOnlyList<Block> blocks, Func<RawXmlBlock, bool> rawIsParagraph)
    {
        for (int i = blocks.Count - 1; i >= 0; i--)
        {
            switch (blocks[i])
            {
                case Block block when IsParagraph(block, rawIsParagraph):
                    return false;
                case ComponentBlock component:
                    return MayEndWithTable(blocks.Take(i).Concat(component.Body).ToList(), rawIsParagraph);
                case ListBlock list:
                    return MayEndWithTable(blocks.Take(i).Concat(list.Items).ToList(), rawIsParagraph);
                case ConditionalBlock conditional:
                    List<List<Block>> branches = conditional.Branches.Select(b => b.Body).Append(conditional.Else ?? []).ToList();
                    if (branches.Any(b => MayEndWithTable(b, rawIsParagraph)))
                    {
                        return true;
                    }

                    if (conditional.Else is not null && branches.All(b => EndsWithParagraph(b, rawIsParagraph)))
                    {
                        return false;
                    }

                    continue;
                case LoopBlock loop:
                    if (MayEndWithTable(loop.Body, rawIsParagraph))
                    {
                        return true;
                    }

                    continue;
                default:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Blocks written as one paragraph; whether a raw block is one, only its writer knows (it renders it once).</summary>
    private static bool IsParagraph(Block block, Func<RawXmlBlock, bool> rawIsParagraph) => block switch
    {
        ParagraphBlock or RuleBlock or TocBlock or PageBreakBlock => true,
        RawXmlBlock raw => rawIsParagraph(raw),
        _ => false,
    };

    /// <summary>Without a writer a raw block may be a table: the conservative answer (used to decide on the style).</summary>
    private static bool MayBeTable(RawXmlBlock raw) => false;

    /// <summary>
    /// Whether the blocks end with a paragraph whatever Templify keeps: a conditional with an else whose branches all end
    /// with one does; a loop (it may be empty) or a conditional without an else leaves the blocks before it last.
    /// </summary>
    public static bool EndsWithParagraph(IReadOnlyList<Block> blocks, Func<RawXmlBlock, bool> rawIsParagraph)
    {
        for (int i = blocks.Count - 1; i >= 0; i--)
        {
            switch (blocks[i])
            {
                case Block block when IsParagraph(block, rawIsParagraph):
                    return true;
                case ComponentBlock component:
                    return EndsWithParagraph(blocks.Take(i).Concat(component.Body).ToList(), rawIsParagraph);
                case ListBlock list:
                    return EndsWithParagraph(blocks.Take(i).Concat(list.Items).ToList(), rawIsParagraph);
                case ConditionalBlock conditional:
                    if (!conditional.Branches.All(b => EndsWithParagraph(b.Body, rawIsParagraph)) || (conditional.Else is { } otherwise && !EndsWithParagraph(otherwise, rawIsParagraph)))
                    {
                        return false;
                    }

                    if (conditional.Else is not null)
                    {
                        return true;
                    }

                    continue;
                case LoopBlock loop:
                    if (!EndsWithParagraph(loop.Body, rawIsParagraph))
                    {
                        return false;
                    }

                    continue;
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Whether any spacer is written for these parts: the style sheet then needs the spacer style.</summary>
    public static bool Used(IEnumerable<IReadOnlyList<Block>> parts)
    {
        foreach (IReadOnlyList<Block> part in parts)
        {
            if (AtPartEnd(part, MayBeTable) || Touches(part))
            {
                return true;
            }

            foreach (TableBlock table in BlockTree.All(part).OfType<TableBlock>())
            {
                if (table.AllRows().SelectMany(r => r.Cells).Any(cell => AtCellEnd(cell.Content, MayBeTable) || Touches(cell.Content)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Touches(IReadOnlyList<Block> blocks)
    {
        List<Block> flattened = BlockFlattener.Flatten(blocks).ToList();
        return flattened.Where((block, i) => After(block, i + 1 < flattened.Count ? flattened[i + 1] : null, MayBeTable)).Any();
    }
}
