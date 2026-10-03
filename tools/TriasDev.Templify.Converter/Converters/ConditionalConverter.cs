// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Models;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Converts conditional content controls (<c>conditionalRemove_*</c>) to Templify if blocks.
/// </summary>
public class ConditionalConverter
{
    /// <summary>
    /// Convert a conditional content control to Templify syntax.
    /// </summary>
    /// <param name="sdt">The content control element.</param>
    /// <param name="tag">The tag value (e.g., "conditionalRemove_process.division").</param>
    /// <returns>True if conversion was successful; false if the tag is not a conditional tag.</returns>
    /// <exception cref="ControlConversionException">The control cannot be converted automatically.</exception>
    public bool Convert(SdtElement sdt, string tag)
    {
        OpenXmlTemplatesTag parsed = OpenXmlTemplatesTag.Parse(tag);
        if (parsed.Type != ControlType.Conditional)
        {
            return false;
        }

        Convert(sdt, parsed, new List<string>());
        return true;
    }

    internal void Convert(SdtElement sdt, OpenXmlTemplatesTag tag, ICollection<string> warnings)
    {
        if (!tag.IsConvertible || tag.Condition?.Expression == null)
        {
            throw new ControlConversionException(string.Join("; ", tag.Errors));
        }

        if (sdt is SdtCell)
        {
            warnings.Add(
                $"{tag.Tag}: cell-level conditional converted to a conditional around the cell content; the (empty) cell itself is kept");
        }

        MarkerPlacement.Wrap(
            sdt,
            $"{{{{#if {tag.Condition.Expression}}}}}",
            "{{/if}}",
            HighlightColorValues.Cyan);
    }
}
