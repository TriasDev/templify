// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// The human-readable location of a validation problem: <c>&lt;part&gt;, &lt;path&gt;: '&lt;text&gt;'</c>, e.g.
/// <c>body, table 1, row 2, cell 3, paragraph 1: 'Intro {{#if Flag}}'</c> (#255).
/// </summary>
internal static class TemplateLocation
{
    /// <summary>Formats a location: the part, the 1-based path segments and the element's text (shortened).</summary>
    public static string Format(string part, IEnumerable<string> path, string text)
    {
        string segments = string.Join(", ", path);
        string where = segments.Length == 0 ? part : $"{part}, {segments}";
        return $"{where}: '{BlockMarkerText.GetLocation(text)}'";
    }
}
