// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;

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
    /// <returns>True if conversion was successful.</returns>
    public bool Convert(SdtElement sdt, string tag)
    {
        if (!tag.StartsWith("variable_"))
        {
            return false;
        }

        // Extract variable path: "variable_process.name" -> "process.name"
        string variablePath = tag.Substring("variable_".Length);

        // Generate Templify placeholder
        string placeholder = $"{{{{{variablePath}}}}}";

        // Replace content with placeholder (no highlighting for variables)
        OpenXmlHelpers.ReplaceContentControlText(sdt, placeholder, highlightColor: null);

        // Unwrap the content control
        OpenXmlHelpers.UnwrapContentControl(sdt);

        return true;
    }
}
