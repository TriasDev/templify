using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Helper methods for working with OpenXML elements.
/// </summary>
public static class OpenXmlHelpers
{
    /// <summary>
    /// Get the tag value from a content control.
    /// </summary>
    public static string? GetContentControlTag(SdtElement sdt)
    {
        return sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
    }

    /// <summary>
    /// Unwrap a content control, moving its contents to replace the control itself.
    /// Handles special cases to prevent invalid XML structures like nested paragraphs.
    /// </summary>
    /// <param name="sdt">The content control to unwrap.</param>
    /// <returns>The last element that was moved, or null if no elements were moved.</returns>
    public static OpenXmlElement? UnwrapContentControl(SdtElement sdt)
    {
        OpenXmlCompositeElement? sdtContent = GetSdtContent(sdt);

        if (sdtContent == null)
        {
            sdt.Remove();
            return null;
        }

        // Check if we're inside a paragraph
        Paragraph? parentParagraph = sdt.Ancestors<Paragraph>().FirstOrDefault();

        // Move all children to before the control
        List<OpenXmlElement> children = sdtContent.ChildElements.ToList();
        OpenXmlElement? lastMovedElement = null;

        foreach (OpenXmlElement child in children)
        {
            OpenXmlElement clonedChild = child.CloneNode(true);

            // Special handling: if the control is inside a paragraph and the child is also a paragraph,
            // we need to move the paragraph's content (runs) instead of the paragraph itself
            // to avoid creating invalid nested paragraphs
            if (parentParagraph != null && clonedChild is Paragraph childParagraph)
            {
                // Move the runs from the child paragraph instead of the paragraph itself
                foreach (Run run in childParagraph.Elements<Run>().ToList())
                {
                    Run clonedRun = run.CloneNode(true) as Run ?? new Run();
                    sdt.InsertBeforeSelf(clonedRun);
                    lastMovedElement = clonedRun;
                }
            }
            else
            {
                sdt.InsertBeforeSelf(clonedChild);
                lastMovedElement = clonedChild;
            }
        }

        // Remove the control
        sdt.Remove();

        return lastMovedElement;
    }

    /// <summary>
    /// Get the content element from an SdtElement (handles different Sdt types).
    /// </summary>
    private static OpenXmlCompositeElement? GetSdtContent(SdtElement sdt)
    {
        if (sdt is SdtBlock block)
        {
            return block.SdtContentBlock;
        }
        else if (sdt is SdtRun run)
        {
            return run.SdtContentRun;
        }
        else if (sdt is SdtCell cell)
        {
            return cell.SdtContentCell;
        }

        return null;
    }

    /// <summary>
    /// Insert text before an element, creating appropriate Run/Paragraph structure.
    /// The inserted text can be optionally highlighted for visibility.
    /// </summary>
    /// <param name="element">The element to insert text before.</param>
    /// <param name="text">The text to insert.</param>
    /// <param name="highlightColor">Optional highlight color. Pass null to disable highlighting.</param>
    public static void InsertTextBefore(OpenXmlElement element, string text, HighlightColorValues? highlightColor = null)
    {
        Run newRun = new Run(new Text(text));

        // Add highlighting if specified
        if (highlightColor.HasValue && highlightColor.Value != HighlightColorValues.None)
        {
            newRun.RunProperties = new RunProperties();
            newRun.RunProperties.Append(new Highlight() { Val = highlightColor.Value });
        }

        // Find the first Run before this element to insert adjacent to it
        Run? previousRun = element.ElementsBefore().OfType<Run>().LastOrDefault();
        if (previousRun != null)
        {
            previousRun.InsertAfterSelf(newRun);
            return;
        }

        // If element is in a Paragraph, insert the Run there
        Paragraph? paragraph = element.Ancestors<Paragraph>().FirstOrDefault();
        if (paragraph != null)
        {
            element.InsertBeforeSelf(newRun);
            return;
        }

        // Otherwise, we need to create a new Paragraph
        Paragraph newPara = new Paragraph(newRun);
        element.InsertBeforeSelf(newPara);
    }

    /// <summary>
    /// Insert text after an element, creating appropriate Run/Paragraph structure.
    /// The inserted text can be optionally highlighted for visibility.
    /// </summary>
    /// <param name="element">The element to insert text after.</param>
    /// <param name="text">The text to insert.</param>
    /// <param name="highlightColor">Optional highlight color. Pass null to disable highlighting.</param>
    public static void InsertTextAfter(OpenXmlElement element, string text, HighlightColorValues? highlightColor = null)
    {
        Run newRun = new Run(new Text(text));

        // Add highlighting if specified
        if (highlightColor.HasValue && highlightColor.Value != HighlightColorValues.None)
        {
            newRun.RunProperties = new RunProperties();
            newRun.RunProperties.Append(new Highlight() { Val = highlightColor.Value });
        }

        // Special handling for SdtElement (content controls)
        // Insert the text INSIDE the content control, after its last child element
        // This ensures the closing tag stays with the content when the control is unwrapped
        if (element is SdtElement sdt)
        {
            OpenXmlCompositeElement? sdtContent = GetSdtContent(sdt);

            if (sdtContent != null)
            {
                // Special case: SdtCell (table cells) may have multiple nested elements
                // We need to handle them differently to ensure the closing tag is in the right place

                // Get all paragraphs in the content (handles nested structures like tables)
                List<Paragraph> paragraphs = sdtContent.Descendants<Paragraph>().ToList();

                if (paragraphs.Count > 0)
                {
                    // Append to the very last paragraph found
                    Paragraph lastPara = paragraphs[paragraphs.Count - 1];
                    lastPara.AppendChild(newRun);
                    return;
                }

                // If no paragraphs found, try to get the last direct child
                OpenXmlElement? lastChild = sdtContent.LastChild;

                if (lastChild != null)
                {
                    // If the last child is a paragraph, append the run to it
                    if (lastChild is Paragraph lastPara)
                    {
                        lastPara.AppendChild(newRun);
                        return;
                    }

                    // If no paragraph found, insert a new paragraph after the last child
                    Paragraph newPara = new Paragraph(newRun);
                    lastChild.InsertAfterSelf(newPara);
                    return;
                }
                else
                {
                    // No children, create a new paragraph in the content
                    Paragraph newPara = new Paragraph(newRun);
                    sdtContent.AppendChild(newPara);
                    return;
                }
            }

            // If we couldn't get content, fall through to default behavior
        }

        // Special handling for Table elements
        // Insert the text in the last cell of the last row
        if (element is Table table)
        {
            var lastRow = table.Descendants<TableRow>().LastOrDefault();
            if (lastRow != null)
            {
                var lastCell = lastRow.Descendants<TableCell>().LastOrDefault();
                if (lastCell != null)
                {
                    // Get the last paragraph in the cell, or create one
                    var lastPara = lastCell.Descendants<Paragraph>().LastOrDefault();
                    if (lastPara != null)
                    {
                        lastPara.AppendChild(newRun);
                        return;
                    }
                    else
                    {
                        Paragraph newPara = new Paragraph(newRun);
                        lastCell.AppendChild(newPara);
                        return;
                    }
                }
            }

            // Fallback: insert a paragraph after the table
            Paragraph fallbackPara = new Paragraph(newRun);
            table.InsertAfterSelf(fallbackPara);
            return;
        }

        // Find the first Run after this element to insert adjacent to it
        Run? nextRun = element.ElementsAfter().OfType<Run>().FirstOrDefault();
        if (nextRun != null)
        {
            nextRun.InsertBeforeSelf(newRun);
            return;
        }

        // If element is in a Paragraph, insert the Run there
        Paragraph? paragraph = element.Ancestors<Paragraph>().FirstOrDefault();
        if (paragraph != null)
        {
            element.InsertAfterSelf(newRun);
            return;
        }

        // Otherwise, we need to create a new Paragraph
        Paragraph newPara2 = new Paragraph(newRun);
        element.InsertAfterSelf(newPara2);
    }

    /// <summary>
    /// Replace the content of a content control with new text, preserving formatting.
    /// The replacement text can be optionally highlighted for visibility.
    /// </summary>
    /// <param name="sdt">The content control to replace text in.</param>
    /// <param name="newText">The new text to insert.</param>
    /// <param name="highlightColor">Optional highlight color. Pass null to disable highlighting.</param>
    public static void ReplaceContentControlText(SdtElement sdt, string newText, HighlightColorValues? highlightColor = null)
    {
        OpenXmlCompositeElement? sdtContent = GetSdtContent(sdt);

        if (sdtContent == null)
        {
            return;
        }

        // Get existing run properties for formatting
        Run? existingRun = sdtContent.Descendants<Run>().FirstOrDefault();
        RunProperties? runProps = existingRun?.RunProperties?.CloneNode(true) as RunProperties;

        // Create new run with text
        Run newRun = new Run(new Text(newText));
        if (runProps != null)
        {
            newRun.RunProperties = runProps;

            // Add highlighting if specified
            if (highlightColor.HasValue && highlightColor.Value != HighlightColorValues.None)
            {
                // Insert highlighting in proper position in the OpenXML schema
                // Schema order: ... color, spacing, w, kern, position, sz, szCs, highlight, u, effect, bdr, shd, ...
                // Strategy: Insert BEFORE elements that must come after highlight
                Highlight highlight = new Highlight() { Val = highlightColor.Value };

                // Find first element that should come AFTER highlight
                OpenXmlElement? insertBefore = newRun.RunProperties.GetFirstChild<Underline>();
                if (insertBefore == null)
                {
                    insertBefore = newRun.RunProperties.GetFirstChild<Shading>();
                }
                if (insertBefore == null)
                {
                    insertBefore = newRun.RunProperties.GetFirstChild<VerticalTextAlignment>();
                }
                if (insertBefore == null)
                {
                    insertBefore = newRun.RunProperties.GetFirstChild<Languages>();
                }

                if (insertBefore != null)
                {
                    insertBefore.InsertBeforeSelf(highlight);
                }
                else
                {
                    // No elements after highlight found, safe to append
                    newRun.RunProperties.AppendChild(highlight);
                }
            }
        }
        else if (highlightColor.HasValue && highlightColor.Value != HighlightColorValues.None)
        {
            // Create new RunProperties with highlighting
            newRun.RunProperties = new RunProperties();
            newRun.RunProperties.Append(new Highlight() { Val = highlightColor.Value });
        }

        // Remove all existing runs
        foreach (Run run in sdtContent.Descendants<Run>().ToList())
        {
            run.Remove();
        }

        // Add new run
        if (sdtContent is SdtContentBlock blockContent)
        {
            Paragraph? para = blockContent.GetFirstChild<Paragraph>();
            if (para == null)
            {
                para = new Paragraph();
                blockContent.AppendChild(para);
            }
            para.AppendChild(newRun);
        }
        else if (sdtContent is SdtContentRun runContent)
        {
            runContent.AppendChild(newRun);
        }
        else if (sdtContent is SdtContentCell cellContent)
        {
            // For table cells, find the TableCell element inside the content
            // and add the placeholder to a paragraph inside it (not at SdtContentCell level)
            // This prevents creating malformed row-level paragraphs when unwrapping
            TableCell? tableCell = cellContent.GetFirstChild<TableCell>();
            if (tableCell != null)
            {
                // Add to existing paragraph in the cell, or create one
                Paragraph? para = tableCell.GetFirstChild<Paragraph>();
                if (para == null)
                {
                    para = new Paragraph();
                    tableCell.AppendChild(para);
                }
                para.AppendChild(newRun);
            }
            else
            {
                // Fallback: if no TableCell found (shouldn't happen), use old behavior
                Paragraph? para = cellContent.GetFirstChild<Paragraph>();
                if (para == null)
                {
                    para = new Paragraph();
                    cellContent.AppendChild(para);
                }
                para.AppendChild(newRun);
            }
        }
    }

    /// <summary>
    /// Get the parent paragraph of an element.
    /// </summary>
    public static Paragraph? GetParentParagraph(OpenXmlElement element)
    {
        return element.Ancestors<Paragraph>().FirstOrDefault();
    }

    /// <summary>
    /// Get the parent table row of an element.
    /// </summary>
    public static TableRow? GetParentTableRow(OpenXmlElement element)
    {
        return element.Ancestors<TableRow>().FirstOrDefault();
    }

    /// <summary>
    /// Check if an element is inside a table.
    /// </summary>
    public static bool IsInTable(OpenXmlElement element)
    {
        return element.Ancestors<Table>().Any();
    }

    /// <summary>
    /// Check if an element is inside a table row.
    /// </summary>
    public static bool IsInTableRow(OpenXmlElement element)
    {
        return element.Ancestors<TableRow>().Any();
    }
}
