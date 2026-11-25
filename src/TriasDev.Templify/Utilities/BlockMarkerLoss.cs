// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// The text a Word loop or block conditional loses with its marker elements (#253): processing removes each marker
/// paragraph / row / content control as a whole. Used by the visitors (warnings) and the validator (errors), on the
/// blocks found by the same detectors.
/// </summary>
internal static class BlockMarkerLoss
{
    public static IEnumerable<(OpenXmlElement Marker, string Message)> Find(LoopBlock loop)
    {
        if (loop.StartMarker == loop.EndMarker)
        {
            string? message = BlockMarkerText.GetInlineLoopMessage(GetText(loop.StartMarker), GetUnit(loop.StartMarker));
            return message == null
                ? Array.Empty<(OpenXmlElement, string)>()
                : new[] { (loop.StartMarker, message) };
        }

        return FindInMarkers(new[] { loop.StartMarker, loop.EndMarker });
    }

    public static IEnumerable<(OpenXmlElement Marker, string Message)> Find(ConditionalBlock conditional)
    {
        // An inline conditional (all markers in one paragraph) is resolved within the paragraph: nothing is lost.
        if (conditional.StartMarker == conditional.EndMarker && conditional.StartMarker is Paragraph)
        {
            return Array.Empty<(OpenXmlElement, string)>();
        }

        return FindInMarkers(conditional.Branches.Select(b => b.Marker).Append(conditional.EndMarker));
    }

    public static string GetText(OpenXmlElement marker) => TemplateElementText.GetOwnText(marker);

    private static IEnumerable<(OpenXmlElement Marker, string Message)> FindInMarkers(IEnumerable<OpenXmlElement> markers)
    {
        foreach (OpenXmlElement marker in markers.Distinct())
        {
            string? message = BlockMarkerText.GetLostTextMessage(GetText(marker), GetUnit(marker));
            if (message != null)
            {
                yield return (marker, message);
            }
        }
    }

    internal static string GetUnit(OpenXmlElement marker) => marker switch
    {
        TableRow => "table row",
        TableCell => "table cell",
        SdtElement => "content control",
        _ => "paragraph",
    };
}
