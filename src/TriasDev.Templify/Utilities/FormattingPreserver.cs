// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Utility class for preserving and applying text formatting (character and paragraph styles).
/// </summary>
internal static class FormattingPreserver
{
    /// <summary>
    /// Clones RunProperties for use in a new run.
    /// Returns null if original properties are null.
    /// </summary>
    public static RunProperties? CloneRunProperties(RunProperties? originalProperties)
    {
        if (originalProperties == null)
        {
            return null;
        }

        return (RunProperties)originalProperties.CloneNode(true);
    }

    /// <summary>
    /// Applies RunProperties to a run.
    /// If properties are null, the run remains without properties.
    /// </summary>
    public static void ApplyRunProperties(Run run, RunProperties? properties)
    {
        if (properties != null)
        {
            run.RunProperties = properties;
        }
    }

    /// <summary>
    /// Applies markdown-style formatting to RunProperties (bold, italic, strikethrough).
    /// Creates new RunProperties if none exist, or modifies existing properties.
    /// Note: This method modifies the passed baseProperties object if non-null.
    /// Callers should pass a cloned copy if they need to preserve the original.
    /// </summary>
    /// <param name="baseProperties">The base RunProperties to modify (can be null).</param>
    /// <param name="isBold">Whether to apply bold formatting.</param>
    /// <param name="isItalic">Whether to apply italic formatting.</param>
    /// <param name="isStrikethrough">Whether to apply strikethrough formatting.</param>
    /// <returns>RunProperties with the markdown formatting applied.</returns>
    public static RunProperties? ApplyMarkdownFormatting(
        RunProperties? baseProperties,
        bool isBold,
        bool isItalic,
        bool isStrikethrough)
    {
        // If no formatting needed, return base properties as-is
        if (!isBold && !isItalic && !isStrikethrough)
        {
            return baseProperties;
        }

        // Create new properties if none exist, or use existing ones (caller should have cloned)
        RunProperties properties = baseProperties ?? new RunProperties();

        // Note: the typed setters (properties.Bold etc.) insert the element at its schema-defined
        // position within w:rPr. Appending would place w:b/w:i/w:strike after w:color/w:sz,
        // which violates the CT_RPr sequence and fails OpenXmlValidator.

        // Apply bold formatting
        if (isBold)
        {
            // Remove existing Bold element if present to avoid duplicates
            properties.RemoveAllChildren<Bold>();
            properties.Bold = new Bold();
        }

        // Apply italic formatting
        if (isItalic)
        {
            // Remove existing Italic element if present to avoid duplicates
            properties.RemoveAllChildren<Italic>();
            properties.Italic = new Italic();
        }

        // Apply strikethrough formatting
        if (isStrikethrough)
        {
            // Remove existing Strike element if present to avoid duplicates
            properties.RemoveAllChildren<Strike>();
            properties.Strike = new Strike();
        }

        return properties;
    }
}
