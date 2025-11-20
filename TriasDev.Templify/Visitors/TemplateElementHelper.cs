// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Provides shared utility methods for working with OpenXML template elements.
/// Eliminates code duplication across detectors and processors.
/// </summary>
/// <remarks>
/// This class consolidates duplicate implementations from:
/// - ConditionalDetector.GetElementText()
/// - LoopDetector.GetElementText()
/// - LoopProcessor.GetElementText()
/// </remarks>
internal static class TemplateElementHelper
{
    /// <summary>
    /// Gets the text content of an element (paragraph, table row, table cell, or table).
    /// </summary>
    /// <param name="element">The element to extract text from.</param>
    /// <returns>The inner text of the element, or null if the element type is not supported.</returns>
    /// <remarks>
    /// Supported element types:
    /// - Paragraph: Returns paragraph.InnerText
    /// - TableRow: Returns row.InnerText
    /// - TableCell: Returns cell.InnerText
    /// - Table: Returns table.InnerText
    ///
    /// This method replaces 3 duplicate implementations:
    /// - ConditionalDetector.GetElementText() (lines 189-207)
    /// - LoopDetector.GetElementText() (lines 150-168)
    /// - LoopProcessor.GetElementText() (lines 548-566)
    /// </remarks>
    public static string? GetElementText(OpenXmlElement element)
    {
        return element switch
        {
            Paragraph paragraph => paragraph.InnerText,
            TableRow row => row.InnerText,
            TableCell cell => cell.InnerText,
            Table table => table.InnerText,
            _ => null
        };
    }

    /// <summary>
    /// Checks if an element contains any template marker (conditional, loop, or placeholder).
    /// </summary>
    /// <param name="element">The element to check.</param>
    /// <returns>True if the element contains "{{", false otherwise.</returns>
    public static bool ContainsTemplateMarker(OpenXmlElement element)
    {
        string? text = GetElementText(element);
        if (text == null)
        {
            return false;
        }

        return text.Contains("{{");
    }

    /// <summary>
    /// Safely removes an element from the document if it has a parent.
    /// </summary>
    /// <param name="element">The element to remove.</param>
    /// <remarks>
    /// Prevents "element has no parent" exceptions when removing already-detached elements.
    /// This is a common pattern in ConditionalProcessor and LoopProcessor where elements
    /// may have been removed by nested processing.
    /// </remarks>
    public static void SafeRemove(OpenXmlElement element)
    {
        if (element.Parent != null)
        {
            element.Remove();
        }
    }

    /// <summary>
    /// Removes multiple elements safely.
    /// </summary>
    /// <param name="elements">The elements to remove.</param>
    public static void SafeRemoveRange(IEnumerable<OpenXmlElement> elements)
    {
        foreach (OpenXmlElement element in elements)
        {
            SafeRemove(element);
        }
    }

    /// <summary>
    /// Clones an OpenXML element deeply (including all descendants).
    /// </summary>
    /// <typeparam name="T">The type of OpenXML element.</typeparam>
    /// <param name="element">The element to clone.</param>
    /// <returns>A deep clone of the element.</returns>
    public static T CloneElement<T>(T element) where T : OpenXmlElement
    {
        return (T)element.CloneNode(true);
    }

    /// <summary>
    /// Clones a list of elements deeply.
    /// </summary>
    /// <param name="elements">The elements to clone.</param>
    /// <returns>A list of deep-cloned elements.</returns>
    public static List<OpenXmlElement> CloneElements(IEnumerable<OpenXmlElement> elements)
    {
        return elements.Select(e => CloneElement(e)).ToList();
    }
}
