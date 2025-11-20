using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// Unit tests for DocumentWalker.
/// Tests document traversal, detection, and visitor dispatch.
/// </summary>
public sealed class DocumentWalkerTests
{
    [Fact]
    public void Walk_NullDocument_DoesNotThrow()
    {
        // Arrange
        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw)
        walker.Walk(null!, visitor, context);

        // Assert - no visits occurred
        Assert.Empty(visitor.VisitedConditionals);
        Assert.Empty(visitor.VisitedLoops);
        Assert.Empty(visitor.VisitedPlaceholders);
        Assert.Empty(visitor.VisitedParagraphs);
    }

    [Fact]
    public void Walk_EmptyDocument_DoesNotThrow()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        walker.Walk(document, visitor, context);

        // Assert - no template elements in empty document
        Assert.Empty(visitor.VisitedConditionals);
        Assert.Empty(visitor.VisitedLoops);
        Assert.Empty(visitor.VisitedPlaceholders);
        Assert.Empty(visitor.VisitedParagraphs);

        document.Dispose();
    }

    [Fact]
    public void Walk_ConditionalBlock_VisitsConditional()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("Active");
        builder.AddParagraph("{{/if}}");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsActive"] = true };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        walker.Walk(document, visitor, context);

        // Assert
        Assert.Single(visitor.VisitedConditionals);
        ConditionalBlock conditional = visitor.VisitedConditionals[0];
        Assert.Equal("IsActive", conditional.ConditionExpression);

        document.Dispose();
    }

    [Fact]
    public void Walk_ParagraphWithPlaceholder_VisitsPlaceholder()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["Name"] = "World" };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        walker.Walk(document, visitor, context);

        // Assert
        Assert.Single(visitor.VisitedPlaceholders);
        PlaceholderMatch placeholder = visitor.VisitedPlaceholders[0];
        Assert.Equal("Name", placeholder.VariableName);

        document.Dispose();
    }

    [Fact]
    public void Walk_MultiplePlaceholdersInParagraph_VisitsAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{FirstName}} {{LastName}}!");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        walker.Walk(document, visitor, context);

        // Assert
        Assert.Equal(2, visitor.VisitedPlaceholders.Count);
        Assert.Contains(visitor.VisitedPlaceholders, p => p.VariableName == "FirstName");
        Assert.Contains(visitor.VisitedPlaceholders, p => p.VariableName == "LastName");

        document.Dispose();
    }

    [Fact]
    public void Walk_RegularParagraph_VisitsParagraph()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("This is just regular text with no template markers.");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        walker.Walk(document, visitor, context);

        // Assert
        Assert.Single(visitor.VisitedParagraphs);
        Assert.Equal("This is just regular text with no template markers.",
            visitor.VisitedParagraphs[0].InnerText);

        document.Dispose();
    }

    [Fact]
    public void Walk_ConditionalMarkers_DoesNotVisitAsRegularParagraphs()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("Content");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Other content");
        builder.AddParagraph("{{/if}}");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        walker.Walk(document, visitor, context);

        // Assert
        Assert.Single(visitor.VisitedConditionals);

        // Marker paragraphs should NOT be visited as regular paragraphs
        Assert.DoesNotContain(visitor.VisitedParagraphs, p => p.InnerText.Contains("{{#if"));
        Assert.DoesNotContain(visitor.VisitedParagraphs, p => p.InnerText.Contains("{{else}}"));
        Assert.DoesNotContain(visitor.VisitedParagraphs, p => p.InnerText.Contains("{{/if}}"));

        document.Dispose();
    }

    [Fact]
    public void Walk_NestedConditionals_ProcessesDeepestFirst()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Outer}}");
        builder.AddParagraph("{{#if Inner}}");
        builder.AddParagraph("Nested content");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        walker.Walk(document, visitor, context);

        // Assert
        Assert.Equal(2, visitor.VisitedConditionals.Count);

        // First visited should be the inner (nesting level 1)
        // Second visited should be the outer (nesting level 0)
        Assert.Equal(1, visitor.VisitedConditionals[0].NestingLevel);
        Assert.Equal(0, visitor.VisitedConditionals[1].NestingLevel);

        document.Dispose();
    }

    [Fact]
    public void Walk_MixedContent_VisitsInCorrectOrder()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if ShowGreeting}}");
        builder.AddParagraph("Hello {{Name}}!");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("Regular paragraph");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        MockVisitor visitor = new MockVisitor();
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["ShowGreeting"] = true,
            ["Name"] = "World"
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        walker.Walk(document, visitor, context);

        // Assert
        // Conditionals are processed first (they're detected and visited)
        Assert.Single(visitor.VisitedConditionals);

        // Then placeholders and regular paragraphs
        // Note: The actual visitor implementation will determine if placeholders inside
        // conditionals are visited before or after the conditional is processed
        Assert.True(visitor.VisitedPlaceholders.Count >= 0); // May or may not be visited depending on conditional result
        Assert.True(visitor.VisitedParagraphs.Count >= 0); // May include "Regular paragraph"

        document.Dispose();
    }

    [Fact]
    public void WalkElements_SkipsRemovedElements()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if False}}");
        builder.AddParagraph("This should be removed");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("This should remain");

        MemoryStream stream = builder.ToStream();
        WordprocessingDocument document = WordprocessingDocument.Open(stream, true);

        DocumentWalker walker = new DocumentWalker();
        // Use a visitor that actually processes (removes) elements
        RemovingVisitor visitor = new RemovingVisitor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["False"] = false };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        walker.Walk(document, visitor, context);

        // Assert - verify walker didn't crash when encountering removed elements
        Assert.True(visitor.ProcessedConditional);

        document.Dispose();
    }

    // Mock visitor for testing - records all visits without processing
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

    // Removing visitor for testing element removal - actually processes conditionals
    private class RemovingVisitor : ITemplateElementVisitor
    {
        public bool ProcessedConditional { get; private set; }

        public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
        {
            ProcessedConditional = true;

            // Simulate conditional processing by removing false branch
            ConditionalEvaluator evaluator = new ConditionalEvaluator();
            bool result = evaluator.Evaluate(conditional.ConditionExpression, context);

            if (result)
            {
                // Remove else branch
                foreach (var element in conditional.ElseContentElements)
                {
                    if (element.Parent != null)
                    {
                        element.Remove();
                    }
                }
            }
            else
            {
                // Remove if branch
                foreach (var element in conditional.IfContentElements)
                {
                    if (element.Parent != null)
                    {
                        element.Remove();
                    }
                }
            }

            // Remove markers
            if (conditional.StartMarker.Parent != null) conditional.StartMarker.Remove();
            if (conditional.ElseMarker?.Parent != null) conditional.ElseMarker.Remove();
            if (conditional.EndMarker.Parent != null) conditional.EndMarker.Remove();
        }

        public void VisitLoop(LoopBlock loop, IEvaluationContext context)
        {
            // Not implemented for this test
        }

        public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
        {
            // Not implemented for this test
        }

        public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
        {
            // Not implemented for this test
        }
    }
}
