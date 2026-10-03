// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using TriasDev.Templify.Converter.Analyzers;
using TriasDev.Templify.Converter.Models;
using static TriasDev.Templify.Converter.Tests.TestDocuments;

namespace TriasDev.Templify.Converter.Tests;

public class TemplateAnalyzerTests
{
    [Fact]
    public void AnalyzeTemplate_UsesSameSyntaxAsConverter_AndCoversHeadersAndFooters()
    {
        using TempDirectory dir = new();
        string path = dir.File("t.docx");
        Create(
            path,
            body: new OpenXmlElement[]
            {
                BlockControl("conditionalRemove_status_eq_gold_or_vip", Para("Premium")),
                BlockControl("conditionalRemove_count_gte_5_not", Para("Few")),
                BlockControl("conditionalRemove_is_active", Para("Active")),
                BlockControl("conditionalRemove_a_or", Para("Broken")),
                BlockControl("repeating_items_separator_, ", Para(InlineVariable("name"))),
                BlockControl("toc", Para("TOC")),
            },
            header: new OpenXmlElement[] { Para(InlineVariable("title")) },
            footer: new OpenXmlElement[] { Para(InlineVariable("page_label")) });

        AnalysisResult result = new TemplateAnalyzer().AnalyzeTemplate(path);

        Dictionary<string, ControlInfo> byTag = result.Controls.ToDictionary(c => c.Tag);
        Assert.Equal(9, result.TotalControls);
        Assert.Equal("{{#if status = \"gold\" or vip}}...{{/if}}", byTag["conditionalRemove_status_eq_gold_or_vip"].TemplifySyntax);
        Assert.Equal("{{#if not count >= 5}}...{{/if}}", byTag["conditionalRemove_count_gte_5_not"].TemplifySyntax);
        Assert.Equal("{{#if is_active}}...{{/if}}", byTag["conditionalRemove_is_active"].TemplifySyntax);
        Assert.Equal("is_active", byTag["conditionalRemove_is_active"].VariablePath);
        Assert.Equal("{{#foreach items}}...{{/foreach}}", byTag["repeating_items_separator_, "].TemplifySyntax);
        Assert.True(byTag["repeating_items_separator_, "].RequiresManualReview);
        Assert.True(byTag["conditionalRemove_a_or"].RequiresManualReview);
        Assert.DoesNotContain("{{#if", byTag["conditionalRemove_a_or"].TemplifySyntax);
        Assert.Equal(ControlType.Unknown, byTag["toc"].Type);
        Assert.StartsWith("Header", byTag["variable_title"].Location);
        Assert.StartsWith("Footer", byTag["variable_page_label"].Location);

        foreach (ControlInfo control in result.Controls)
        {
            Assert.DoesNotContain(" eq ", control.TemplifySyntax);
            Assert.DoesNotContain(" gt ", control.TemplifySyntax);
            Assert.DoesNotContain(" gte ", control.TemplifySyntax);
        }

        string report = result.GenerateMarkdownReport();
        Assert.Contains("conditionalRemove_a_or", report);
    }

    [Fact]
    public void AnalyzeTemplate_VariableIndexInsideRepeating_SuggestsLoopNumber()
    {
        using TempDirectory dir = new();
        string path = dir.File("index.docx");
        Create(path, new OpenXmlElement[]
        {
            BlockControl("repeating_items", Para(InlineVariable("index"))),
        });

        AnalysisResult result = new TemplateAnalyzer().AnalyzeTemplate(path);

        Assert.Equal("{{@number}}", result.Controls.Single(c => c.Tag == "variable_index").TemplifySyntax);
    }
}
