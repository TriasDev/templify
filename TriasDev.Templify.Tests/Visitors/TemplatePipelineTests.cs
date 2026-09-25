// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// Tests for the internal <see cref="TemplatePipeline"/> factory (visitor graph wiring).
/// </summary>
public sealed class TemplatePipelineTests
{
    [Fact]
    public void Create_WiresLoopVisitorToFullComposite_NestedLoopsAndConditionalsAreProcessed()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Groups}}");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if Show}}{{Name}}{{/if}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddHeader("Header {{Title}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "T",
            ["Groups"] = new List<Dictionary<string, object>>
            {
                new() { ["Items"] = new List<Dictionary<string, object>> { new() { ["Name"] = "a", ["Show"] = true }, new() { ["Name"] = "b", ["Show"] = false } } },
                new() { ["Items"] = new List<Dictionary<string, object>> { new() { ["Name"] = "c", ["Show"] = true } } },
            },
        };

        HashSet<string> missingVariables = new HashSet<string>();
        WarningCollector warnings = new WarningCollector();
        TemplatePipeline pipeline = TemplatePipeline.Create(new PlaceholderReplacementOptions(), missingVariables, warnings);

        using MemoryStream stream = builder.ToStream();
        using (WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: true))
        {
            pipeline.Process(document, new GlobalEvaluationContext(data));

            List<string> paragraphs = document.MainDocumentPart!.Document!.Body!
                .Elements<Paragraph>()
                .Select(p => p.InnerText)
                .ToList();
            Assert.Equal(new[] { "a", "", "c" }, paragraphs);

            string header = string.Concat(document.MainDocumentPart.HeaderParts.Select(h => h.Header!.InnerText));
            Assert.Equal("Header T", header);
        }

        Assert.Empty(missingVariables);
        Assert.Empty(warnings.GetWarnings());
        Assert.Equal(3, pipeline.PlaceholderVisitor.ReplacementCount);
    }
}
