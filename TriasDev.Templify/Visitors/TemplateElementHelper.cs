// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Provides shared utility methods for removing and cloning OpenXML template elements.
/// </summary>
/// <remarks>
/// Marker text extraction lives in <see cref="Utilities.TemplateElementText"/>.
/// </remarks>
internal static class TemplateElementHelper
{
    /// <summary>
    /// Safely removes an element from the document if it has a parent.
    /// </summary>
    /// <param name="element">The element to remove.</param>
    /// <remarks>
    /// Prevents "element has no parent" exceptions when removing already-detached elements.
    /// This is a common pattern in the conditional and loop visitors where elements
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
