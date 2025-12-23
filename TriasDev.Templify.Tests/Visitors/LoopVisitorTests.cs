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
/// Unit tests for LoopVisitor.
/// Tests loop expansion, context creation, and nested construct processing.
/// </summary>
public sealed class LoopVisitorTests
{
    [Fact]
    public void VisitLoop_EmptyCollection_RemovesLoopBlock()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content = new Paragraph(new Run(new Text("Item: {{.}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor nestedVisitor = new MockVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string>() // Empty collection
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitLoop(loop, context);

        // Assert - all content removed
        Assert.Empty(body.Elements<Paragraph>());
    }

    [Fact]
    public void VisitLoop_MissingCollection_RemovesLoopBlock()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content = new Paragraph(new Run(new Text("Item: {{.}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor nestedVisitor = new MockVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitLoop(loop, context);

        // Assert - all content removed
        Assert.Empty(body.Elements<Paragraph>());
    }

    [Fact]
    public void VisitLoop_NonCollectionVariable_ThrowsInvalidOperationException()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content = new Paragraph(new Run(new Text("Item: {{.}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor nestedVisitor = new MockVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = "Not a collection" // String, not IEnumerable
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            visitor.VisitLoop(loop, context));

        Assert.Contains("Items", exception.Message);
        Assert.Contains("not a collection", exception.Message);
    }

    [Fact]
    public void VisitLoop_SimpleCollection_ExpandsLoopContent()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content = new Paragraph(new Run(new Text("Item")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor nestedVisitor = new MockVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "A", "B", "C" }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitLoop(loop, context);

        // Assert - 3 copies of content, markers removed
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Equal(3, remaining.Count);
        Assert.All(remaining, p => Assert.Equal("Item", p.InnerText));
    }

    [Fact]
    public void VisitLoop_MultipleContentElements_ClonesAll()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content1 = new Paragraph(new Run(new Text("Line 1")));
        Paragraph content2 = new Paragraph(new Run(new Text("Line 2")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content1, content2, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content1, content2 },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor nestedVisitor = new MockVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "A", "B" }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitLoop(loop, context);

        // Assert - 2 iterations * 2 content elements = 4 paragraphs
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Equal(4, remaining.Count);

        // First iteration
        Assert.Equal("Line 1", remaining[0].InnerText);
        Assert.Equal("Line 2", remaining[1].InnerText);

        // Second iteration
        Assert.Equal("Line 1", remaining[2].InnerText);
        Assert.Equal("Line 2", remaining[3].InnerText);
    }

    [Fact]
    public void VisitLoop_CreatesLoopEvaluationContext()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content = new Paragraph(new Run(new Text("Item")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        ContextCapturingVisitor nestedVisitor = new ContextCapturingVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "A", "B" }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitLoop(loop, context);

        // Assert - nested visitor was called with LoopEvaluationContext
        Assert.Equal(2, nestedVisitor.CapturedContexts.Count);
        Assert.All(nestedVisitor.CapturedContexts, ctx => Assert.IsType<LoopEvaluationContext>(ctx));
    }

    [Fact]
    public void VisitLoop_ProcessesNestedConstructsViaDocumentWalker()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content = new Paragraph(new Run(new Text("Item: {{.}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor nestedVisitor = new MockVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "A", "B", "C" }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitLoop(loop, context);

        // Assert - nested visitor processed placeholders (via DocumentWalker)
        // Each cloned iteration should have been walked
        // The paragraph contains {{.}} placeholder, so it will be visited as a placeholder
        Assert.True(nestedVisitor.VisitedPlaceholders.Count >= 3,
            $"Expected at least 3 placeholders visited, but got {nestedVisitor.VisitedPlaceholders.Count}");
    }

    [Fact]
    public void VisitLoop_SingleItem_CreatesOneIteration()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph content = new Paragraph(new Run(new Text("Item")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));

        body.Append(startMarker, content, endMarker);

        LoopBlock loop = new LoopBlock(
            collectionName: "Items",
            iterationVariableName: null,
            contentElements: new List<OpenXmlElement> { content },
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor nestedVisitor = new MockVisitor();
        LoopVisitor visitor = new LoopVisitor(walker, nestedVisitor);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Single" }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitLoop(loop, context);

        // Assert
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Single(remaining);
        Assert.Equal("Item", remaining[0].InnerText);
    }

    [Fact]
    public void VisitConditional_DoesNothing()
    {
        // Arrange
        LoopVisitor visitor = new LoopVisitor(new DocumentWalker(), new MockVisitor());
        ConditionalBlock conditional = CreateTestConditionalBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitConditional(conditional, context);

        // Assert - no exception, no-op completed
        Assert.NotNull(conditional);
    }

    [Fact]
    public void VisitPlaceholder_DoesNothing()
    {
        // Arrange
        LoopVisitor visitor = new LoopVisitor(new DocumentWalker(), new MockVisitor());
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
        LoopVisitor visitor = new LoopVisitor(new DocumentWalker(), new MockVisitor());
        Paragraph paragraph = new Paragraph(new Run(new Text("Regular text")));
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitParagraph(paragraph, context);

        // Assert - paragraph unchanged
        Assert.Equal("Regular text", paragraph.InnerText);
    }

    // Helper methods and mock visitors

    private static ConditionalBlock CreateTestConditionalBlock()
    {
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));
        List<OpenXmlElement> ifContent = new List<OpenXmlElement>
        {
            new Paragraph(new Run(new Text("Active")))
        };

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
    /// Mock visitor that records all visits without processing.
    /// </summary>
    private class MockVisitor : ITemplateElementVisitor
    {
        public List<ConditionalBlock> VisitedConditionals { get; } = new List<ConditionalBlock>();
        public List<LoopBlock> VisitedLoops { get; } = new List<LoopBlock>();
        public List<PlaceholderMatch> VisitedPlaceholders { get; } = new List<PlaceholderMatch>();
        public List<Paragraph> VisitedParagraphs { get; } = new List<Paragraph>();

        public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
        {
            VisitedConditionals.Add(conditional);
        }

        public void VisitLoop(LoopBlock loop, IEvaluationContext context)
        {
            VisitedLoops.Add(loop);
        }

        public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
        {
            VisitedPlaceholders.Add(placeholder);
        }

        public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
        {
            VisitedParagraphs.Add(paragraph);
        }
    }

    /// <summary>
    /// Visitor that captures contexts for testing.
    /// </summary>
    private class ContextCapturingVisitor : ITemplateElementVisitor
    {
        public List<IEvaluationContext> CapturedContexts { get; } = new List<IEvaluationContext>();

        public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
        {
            CapturedContexts.Add(context);
        }

        public void VisitLoop(LoopBlock loop, IEvaluationContext context)
        {
            CapturedContexts.Add(context);
        }

        public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
        {
            CapturedContexts.Add(context);
        }

        public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
        {
            CapturedContexts.Add(context);
        }
    }
}
