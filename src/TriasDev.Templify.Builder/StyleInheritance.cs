// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Gives every paragraph without a style of its own the style it inherits: in a table cell the row's text style,
/// then the table style's header or body text style; everywhere the style sheet's default paragraph style. List items
/// keep their list style's item style. Runs once after composition, so writers see explicit styles only.
/// </summary>
internal static class StyleInheritance
{
    /// <summary>Applies the inherited styles to the body and every header and footer part.</summary>
    public static void Apply(List<Block> body, PageSetup page, StyleSheet styles, BuildContext context)
    {
        foreach (List<Block>? part in new[] { body, page.Header, page.Footer, page.FirstHeader, page.FirstFooter })
        {
            if (part != null)
            {
                Apply(part, styles.DefaultParagraphStyle, styles, context);
            }
        }
    }

    /// <summary>Applies <paramref name="inherited"/> to the unstyled paragraphs of a container, recursively.</summary>
    public static void Apply(List<Block> blocks, ParagraphStyleKey? inherited, StyleSheet styles, BuildContext context)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            switch (blocks[i])
            {
                case ParagraphBlock { Style: null, List: null } paragraph when inherited != null:
                    ParagraphBlock styled = paragraph with { Style = inherited };
                    if (context.Locations.TryGetValue(paragraph, out string? location))
                    {
                        context.Locations.AddOrUpdate(styled, location);
                    }

                    blocks[i] = styled;
                    break;
                case ConditionalBlock conditional:
                    foreach (ConditionalBranch branch in conditional.Branches)
                    {
                        Apply(branch.Body, inherited, styles, context);
                    }

                    if (conditional.Else != null)
                    {
                        Apply(conditional.Else, inherited, styles, context);
                    }

                    break;
                case LoopBlock loop:
                    Apply(loop.Body, inherited, styles, context);
                    break;
                case ComponentBlock component:
                    Apply(component.Body, inherited, styles, context);
                    break;
                case ListBlock list:
                    Apply(list.Items, inherited, styles, context);
                    break;
                case TableBlock table:
                    foreach (TableRowModel row in table.AllRows())
                    {
                        ParagraphStyleKey? cellStyle = ForCell(styles, table, row);
                        foreach (TableCellModel cell in row.Cells)
                        {
                            Apply(cell.Content, cellStyle, styles, context);
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>The style a paragraph in a cell of <paramref name="row"/> inherits.</summary>
    public static ParagraphStyleKey? ForCell(StyleSheet styles, TableBlock table, TableRowModel row)
    {
        TableStyleDefinition? style = styles.Resolve(table);
        return row.TextStyle ?? (row.IsHeader ? style?.HeaderTextStyle : style?.TextStyle) ?? styles.DefaultParagraphStyle;
    }
}
