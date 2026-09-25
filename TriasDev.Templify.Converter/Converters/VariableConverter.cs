// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Models;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Converts variable content controls to Templify placeholders.
/// </summary>
public class VariableConverter
{
    /// <summary>
    /// Convert a variable content control to Templify syntax.
    /// </summary>
    /// <param name="sdt">The content control element.</param>
    /// <param name="tag">The tag value (e.g., "variable_process.name").</param>
    /// <returns>True if conversion was successful; false if the tag is not a variable tag.</returns>
    /// <exception cref="ControlConversionException">The control cannot be converted automatically.</exception>
    public bool Convert(SdtElement sdt, string tag)
    {
        OpenXmlTemplatesTag parsed = OpenXmlTemplatesTag.Parse(tag);
        if (parsed.Type != ControlType.Variable)
        {
            return false;
        }

        Convert(sdt, parsed);
        return true;
    }

    internal void Convert(SdtElement sdt, OpenXmlTemplatesTag tag)
    {
        if (!tag.IsConvertible || tag.TemplifySyntax == null)
        {
            throw new ControlConversionException(string.Join("; ", tag.Errors));
        }

        if (sdt is SdtRow)
        {
            throw new ControlConversionException("A variable control that wraps a whole table row cannot be converted to a placeholder");
        }

        if (!OpenXmlHelpers.ReplaceContentControlText(sdt, tag.TemplifySyntax))
        {
            throw new ControlConversionException("The control has no content that can hold a placeholder");
        }

        OpenXmlHelpers.UnwrapContentControl(sdt);
    }
}
