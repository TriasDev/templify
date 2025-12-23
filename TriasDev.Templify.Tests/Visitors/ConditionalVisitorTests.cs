// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// Unit tests for ConditionalVisitor.
/// Tests conditional evaluation and branch removal.
/// </summary>
public sealed class ConditionalVisitorTests
{
    [Fact]
    public void VisitConditional_TrueCondition_KeepsIfBranchRemovesElse()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph ifContent = new Paragraph(new Run(new Text("Active")));
        Paragraph elseMarker = new Paragraph(new Run(new Text("{{#else}}")));
        Paragraph elseContent = new Paragraph(new Run(new Text("Inactive")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));

        body.Append(startMarker, ifContent, elseMarker, elseContent, endMarker);

        ConditionalBlock conditional = new ConditionalBlock(
            conditionExpression: "IsActive",
            ifContentElements: new List<OpenXmlElement> { ifContent },
            elseContentElements: new List<OpenXmlElement> { elseContent },
            startMarker: startMarker,
            elseMarker: elseMarker,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);

        ConditionalVisitor visitor = new ConditionalVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsActive"] = true };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitConditional(conditional, context);

        // Assert
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Single(remaining);
        Assert.Equal("Active", remaining[0].InnerText);

        // Markers and else branch should be removed
        Assert.Null(startMarker.Parent);
        Assert.Null(elseMarker.Parent);
        Assert.Null(elseContent.Parent);
        Assert.Null(endMarker.Parent);
    }

    [Fact]
    public void VisitConditional_FalseCondition_KeepsElseBranchRemovesIf()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph ifContent = new Paragraph(new Run(new Text("Active")));
        Paragraph elseMarker = new Paragraph(new Run(new Text("{{#else}}")));
        Paragraph elseContent = new Paragraph(new Run(new Text("Inactive")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));

        body.Append(startMarker, ifContent, elseMarker, elseContent, endMarker);

        ConditionalBlock conditional = new ConditionalBlock(
            conditionExpression: "IsActive",
            ifContentElements: new List<OpenXmlElement> { ifContent },
            elseContentElements: new List<OpenXmlElement> { elseContent },
            startMarker: startMarker,
            elseMarker: elseMarker,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);

        ConditionalVisitor visitor = new ConditionalVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsActive"] = false };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitConditional(conditional, context);

        // Assert
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Single(remaining);
        Assert.Equal("Inactive", remaining[0].InnerText);

        // Markers and if branch should be removed
        Assert.Null(startMarker.Parent);
        Assert.Null(ifContent.Parent);
        Assert.Null(elseMarker.Parent);
        Assert.Null(endMarker.Parent);
    }

    [Fact]
    public void VisitConditional_NoElseBranch_TrueCondition_KeepsIfContent()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph ifContent = new Paragraph(new Run(new Text("Active")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));

        body.Append(startMarker, ifContent, endMarker);

        ConditionalBlock conditional = new ConditionalBlock(
            conditionExpression: "IsActive",
            ifContentElements: new List<OpenXmlElement> { ifContent },
            elseContentElements: new List<OpenXmlElement>(),
            startMarker: startMarker,
            elseMarker: null,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);

        ConditionalVisitor visitor = new ConditionalVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsActive"] = true };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitConditional(conditional, context);

        // Assert
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Single(remaining);
        Assert.Equal("Active", remaining[0].InnerText);
    }

    [Fact]
    public void VisitConditional_NoElseBranch_FalseCondition_RemovesAllContent()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph ifContent = new Paragraph(new Run(new Text("Active")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));

        body.Append(startMarker, ifContent, endMarker);

        ConditionalBlock conditional = new ConditionalBlock(
            conditionExpression: "IsActive",
            ifContentElements: new List<OpenXmlElement> { ifContent },
            elseContentElements: new List<OpenXmlElement>(),
            startMarker: startMarker,
            elseMarker: null,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);

        ConditionalVisitor visitor = new ConditionalVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsActive"] = false };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitConditional(conditional, context);

        // Assert - all content removed
        Assert.Empty(body.Elements<Paragraph>());
    }

    [Fact]
    public void VisitConditional_ComparisonOperator_EvaluatesCorrectly()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if Count > 5}}")));
        Paragraph ifContent = new Paragraph(new Run(new Text("Many")));
        Paragraph elseMarker = new Paragraph(new Run(new Text("{{#else}}")));
        Paragraph elseContent = new Paragraph(new Run(new Text("Few")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));

        body.Append(startMarker, ifContent, elseMarker, elseContent, endMarker);

        ConditionalBlock conditional = new ConditionalBlock(
            conditionExpression: "Count > 5",
            ifContentElements: new List<OpenXmlElement> { ifContent },
            elseContentElements: new List<OpenXmlElement> { elseContent },
            startMarker: startMarker,
            elseMarker: elseMarker,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);

        ConditionalVisitor visitor = new ConditionalVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["Count"] = 10 };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitConditional(conditional, context);

        // Assert
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Single(remaining);
        Assert.Equal("Many", remaining[0].InnerText);
    }

    [Fact]
    public void VisitConditional_LoopMetadata_EvaluatesWithLoopContext()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if @first}}")));
        Paragraph ifContent = new Paragraph(new Run(new Text("First Item")));
        Paragraph elseMarker = new Paragraph(new Run(new Text("{{#else}}")));
        Paragraph elseContent = new Paragraph(new Run(new Text("Other Item")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));

        body.Append(startMarker, ifContent, elseMarker, elseContent, endMarker);

        ConditionalBlock conditional = new ConditionalBlock(
            conditionExpression: "@first",
            ifContentElements: new List<OpenXmlElement> { ifContent },
            elseContentElements: new List<OpenXmlElement> { elseContent },
            startMarker: startMarker,
            elseMarker: elseMarker,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);

        ConditionalVisitor visitor = new ConditionalVisitor();

        // Create loop context (simulating first item in loop)
        LoopContext loopContext = new LoopContext("Item1", 0, 3, "Items");
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        LoopEvaluationContext context = new LoopEvaluationContext(loopContext, globalContext);

        // Act
        visitor.VisitConditional(conditional, context);

        // Assert
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Single(remaining);
        Assert.Equal("First Item", remaining[0].InnerText);
    }

    [Fact]
    public void VisitConditional_MultipleIfContentElements_RemovesAll()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if False}}")));
        Paragraph ifContent1 = new Paragraph(new Run(new Text("Line 1")));
        Paragraph ifContent2 = new Paragraph(new Run(new Text("Line 2")));
        Paragraph ifContent3 = new Paragraph(new Run(new Text("Line 3")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));

        body.Append(startMarker, ifContent1, ifContent2, ifContent3, endMarker);

        ConditionalBlock conditional = new ConditionalBlock(
            conditionExpression: "False",
            ifContentElements: new List<OpenXmlElement> { ifContent1, ifContent2, ifContent3 },
            elseContentElements: new List<OpenXmlElement>(),
            startMarker: startMarker,
            elseMarker: null,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);

        ConditionalVisitor visitor = new ConditionalVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["False"] = false };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitConditional(conditional, context);

        // Assert - all content removed
        Assert.Empty(body.Elements<Paragraph>());
    }

    [Fact]
    public void VisitLoop_DoesNothing()
    {
        // Arrange
        ConditionalVisitor visitor = new ConditionalVisitor();
        LoopBlock loop = CreateTestLoopBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitLoop(loop, context);

        // Assert - no exception, no-op completed
        Assert.NotNull(loop);
    }

    [Fact]
    public void VisitPlaceholder_DoesNothing()
    {
        // Arrange
        ConditionalVisitor visitor = new ConditionalVisitor();
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 0,
            Length = 8
        };
        Paragraph paragraph = new Paragraph(new Run(new Text("{{Name}}")));
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert - paragraph unchanged
        Assert.Equal("{{Name}}", paragraph.InnerText);
    }

    [Fact]
    public void VisitParagraph_DoesNothing()
    {
        // Arrange
        ConditionalVisitor visitor = new ConditionalVisitor();
        Paragraph paragraph = new Paragraph(new Run(new Text("Regular text")));
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitParagraph(paragraph, context);

        // Assert - paragraph unchanged
        Assert.Equal("Regular text", paragraph.InnerText);
    }

    // Helper method to create test loop block
    private static LoopBlock CreateTestLoopBlock()
    {
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));
        List<OpenXmlElement> content = new List<OpenXmlElement>
        {
            new Paragraph(new Run(new Text("{{.}}")))
        };

        return new LoopBlock(
            collectionName: "Items",
            contentElements: content,
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);
    }
}
