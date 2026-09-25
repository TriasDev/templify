// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Models;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Converts repeating content controls (<c>repeating_*</c>) to Templify foreach loops.
/// </summary>
/// <remarks>
/// Templify loops are block-level: the markers must be in their own paragraphs (or, for table row
/// loops, in their own rows). Inline (run-level) and cell-level repeating controls therefore cannot be
/// converted automatically and are reported as errors.
/// </remarks>
public class RepeatingConverter
{
    /// <summary>
    /// Convert a repeating content control to Templify syntax.
    /// </summary>
    /// <param name="sdt">The content control element.</param>
    /// <param name="tag">The tag value (e.g., "repeating_process.organisations.items").</param>
    /// <returns>True if conversion was successful; false if the tag is not a repeating tag.</returns>
    /// <exception cref="ControlConversionException">The control cannot be converted automatically.</exception>
    public bool Convert(SdtElement sdt, string tag)
    {
        OpenXmlTemplatesTag parsed = OpenXmlTemplatesTag.Parse(tag);
        if (parsed.Type != ControlType.Repeating)
        {
            return false;
        }

        Convert(sdt, parsed);
        return true;
    }

    internal void Convert(SdtElement sdt, OpenXmlTemplatesTag tag)
    {
        if (!tag.IsConvertible)
        {
            throw new ControlConversionException(string.Join("; ", tag.Errors));
        }

        if (sdt is SdtRun || sdt.Ancestors<Paragraph>().Any())
        {
            throw new ControlConversionException(
                "Inline repeating controls cannot be converted: Templify loops must span whole paragraphs or table rows");
        }

        if (sdt is SdtCell)
        {
            throw new ControlConversionException(
                "Repeating table cells cannot be converted: Templify can repeat paragraphs or table rows, not cells");
        }

        MarkerPlacement.Wrap(
            sdt,
            $"{{{{#foreach {tag.VariablePath}}}}}",
            "{{/foreach}}",
            HighlightColorValues.Green);
    }
}
