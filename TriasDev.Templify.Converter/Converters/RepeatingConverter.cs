// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using HighlightColorValues = DocumentFormat.OpenXml.Wordprocessing.HighlightColorValues;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Converts repeating content controls to Templify foreach loops.
/// </summary>
public class RepeatingConverter
{
    private readonly VariableConverter _variableConverter = new();

    /// <summary>
    /// Convert a repeating content control to Templify syntax.
    /// </summary>
    /// <param name="sdt">The content control element.</param>
    /// <param name="tag">The tag value (e.g., "repeating_process.organisations.items").</param>
    /// <returns>True if conversion was successful.</returns>
    public bool Convert(SdtElement sdt, string tag)
    {
        if (!tag.StartsWith("repeating_"))
        {
            return false;
        }

        // Extract collection path: "repeating_process.organisations.items" -> "process.organisations.items"
        string collectionPath = tag.Substring("repeating_".Length);

        // Insert {{#foreach collectionPath}} before the control with green highlighting
        OpenXmlHelpers.InsertTextBefore(sdt, $"{{{{#foreach {collectionPath}}}}}", HighlightColorValues.Green);

        // Convert inner variable controls BEFORE unwrapping
        // NOTE: In foreach loops, inner controls use relative paths (e.g., "variable_name" not "variable_items.name")
        List<SdtElement> innerControls = sdt.Descendants<SdtElement>()
            .Where(s => s != sdt)
            .ToList();

        foreach (SdtElement innerSdt in innerControls)
        {
            string? innerTag = OpenXmlHelpers.GetContentControlTag(innerSdt);
            if (innerTag == null) continue;

            // Convert inner variables (relative to loop item)
            if (innerTag.StartsWith("variable_"))
            {
                _variableConverter.Convert(innerSdt, innerTag);
            }
            // Note: Inner conditionals and nested repeating will be handled in subsequent passes
        }

        // Unwrap the outer control and get the last moved element
        OpenXmlElement? lastMovedElement = OpenXmlHelpers.UnwrapContentControl(sdt);

        // Insert {{/foreach}} after the last moved element
        // To avoid collision with nested loop end markers, always create a new paragraph
        if (lastMovedElement != null)
        {
            // Create a new paragraph with the end marker
            Paragraph endParagraph = new Paragraph(
                new Run(
                    new RunProperties(new Highlight() { Val = HighlightColorValues.Green }),
                    new Text("{{/foreach}}")
                )
            );

            lastMovedElement.InsertAfterSelf(endParagraph);
        }

        return true;
    }
}
