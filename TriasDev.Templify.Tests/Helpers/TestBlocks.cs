// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;

namespace TriasDev.Templify.Tests.Helpers;

/// <summary>
/// Template blocks for visitor unit tests. Without a container, the markers and content are detached.
/// With a container, they are appended to it in document order, so a test can compare the container's XML.
/// Import with <c>using static</c>.
/// </summary>
internal static class TestBlocks
{
    /// <summary>
    /// Creates <c>{{#if IsActive}}Active{{/if}}</c> without an else branch.
    /// </summary>
    public static ConditionalBlock CreateTestConditionalBlock(OpenXmlElement? container = null)
    {
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));
        List<OpenXmlElement> ifContent = new List<OpenXmlElement>
        {
            new Paragraph(new Run(new Text("Active")))
        };
        container?.Append(startMarker, ifContent[0], endMarker);

        return new ConditionalBlock(
            conditionExpression: "IsActive",
            ifContentElements: ifContent,
            elseContentElements: new List<OpenXmlElement>(),
            startMarker: startMarker,
            elseMarker: null,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);
    }

    /// <summary>
    /// Creates <c>{{#foreach Items}}{{.}}{{/foreach}}</c>.
    /// </summary>
    public static LoopBlock CreateTestLoopBlock(OpenXmlElement? container = null)
    {
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));
        List<OpenXmlElement> content = new List<OpenXmlElement>
        {
            new Paragraph(new Run(new Text("{{.}}")))
        };
        container?.Append(startMarker, content[0], endMarker);

        return new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: content,
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false);
    }
}
