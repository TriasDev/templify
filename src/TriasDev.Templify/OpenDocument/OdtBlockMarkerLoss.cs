// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// The text an OpenDocument loop or block conditional loses with its marker elements (#253): the engine removes each
/// marker paragraph / row / list item as a whole. The counterpart of <c>BlockMarkerLoss</c>, with the same messages.
/// </summary>
internal static class OdtBlockMarkerLoss
{
    public static IEnumerable<(XElement Marker, string Message)> Find(OdtLoopBlock loop)
    {
        if (loop.StartMarker == loop.EndMarker)
        {
            string? message = BlockMarkerText.GetInlineLoopMessage(GetText(loop.StartMarker), GetUnit(loop.StartMarker));
            return message == null
                ? Array.Empty<(XElement, string)>()
                : new[] { (loop.StartMarker, message) };
        }

        return FindInMarkers(new[] { loop.StartMarker, loop.EndMarker });
    }

    public static IEnumerable<(XElement Marker, string Message)> Find(OdtConditionalBlock conditional)
    {
        // An inline conditional (all markers in one paragraph) is resolved within the paragraph: nothing is lost.
        if (conditional.StartMarker == conditional.EndMarker && OdfNames.IsParagraph(conditional.StartMarker))
        {
            return Array.Empty<(XElement, string)>();
        }

        return FindInMarkers(conditional.Branches.Select(b => b.Marker).Append(conditional.EndMarker));
    }

    public static string GetText(XElement marker) => OdtMarkerText.GetOwnText(marker);

    private static IEnumerable<(XElement Marker, string Message)> FindInMarkers(IEnumerable<XElement> markers)
    {
        foreach (XElement marker in markers.Distinct())
        {
            string? message = BlockMarkerText.GetLostTextMessage(GetText(marker), GetUnit(marker));
            if (message != null)
            {
                yield return (marker, message);
            }
        }
    }

    private static string GetUnit(XElement marker)
    {
        if (OdtMarkerText.IsRow(marker))
        {
            return "table row";
        }

        if (OdtMarkerText.IsCell(marker))
        {
            return "table cell";
        }

        return marker.Name == OdfNames.ListItem || marker.Name == OdfNames.ListHeader || OdtMarkerText.IsSingleItemList(marker)
            ? "list item"
            : "paragraph";
    }
}
