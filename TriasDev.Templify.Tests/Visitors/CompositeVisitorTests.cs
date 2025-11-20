// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// Unit tests for CompositeVisitor.
/// Tests visitor composition and dispatch to multiple child visitors.
/// </summary>
public sealed class CompositeVisitorTests
{
    [Fact]
    public void Constructor_NoVisitors_ThrowsArgumentException()
    {
        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CompositeVisitor());

        Assert.Contains("At least one visitor", exception.Message);
    }

    [Fact]
    public void Constructor_NullVisitors_ThrowsArgumentNullException()
    {
        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            new CompositeVisitor((IEnumerable<ITemplateElementVisitor>)null!));

        Assert.Equal("visitors", exception.ParamName);
    }

    [Fact]
    public void Constructor_EmptyVisitorList_ThrowsArgumentException()
    {
        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CompositeVisitor(new List<ITemplateElementVisitor>()));

        Assert.Contains("At least one visitor", exception.Message);
    }

    [Fact]
    public void VisitConditional_DispatchesToAllVisitors()
    {
        // Arrange
        MockVisitor visitor1 = new MockVisitor();
        MockVisitor visitor2 = new MockVisitor();
        MockVisitor visitor3 = new MockVisitor();

        CompositeVisitor composite = new CompositeVisitor(visitor1, visitor2, visitor3);

        ConditionalBlock conditional = CreateTestConditionalBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        composite.VisitConditional(conditional, context);

        // Assert
        Assert.Single(visitor1.VisitedConditionals);
        Assert.Single(visitor2.VisitedConditionals);
        Assert.Single(visitor3.VisitedConditionals);

        Assert.Same(conditional, visitor1.VisitedConditionals[0]);
        Assert.Same(conditional, visitor2.VisitedConditionals[0]);
        Assert.Same(conditional, visitor3.VisitedConditionals[0]);
    }

    [Fact]
    public void VisitLoop_DispatchesToAllVisitors()
    {
        // Arrange
        MockVisitor visitor1 = new MockVisitor();
        MockVisitor visitor2 = new MockVisitor();

        CompositeVisitor composite = new CompositeVisitor(visitor1, visitor2);

        LoopBlock loop = CreateTestLoopBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        composite.VisitLoop(loop, context);

        // Assert
        Assert.Single(visitor1.VisitedLoops);
        Assert.Single(visitor2.VisitedLoops);

        Assert.Same(loop, visitor1.VisitedLoops[0]);
        Assert.Same(loop, visitor2.VisitedLoops[0]);
    }

    [Fact]
    public void VisitPlaceholder_DispatchesToAllVisitors()
    {
        // Arrange
        MockVisitor visitor1 = new MockVisitor();
        MockVisitor visitor2 = new MockVisitor();

        CompositeVisitor composite = new CompositeVisitor(visitor1, visitor2);

        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 0,
            Length = 8
        };
        Paragraph paragraph = new Paragraph(new Run(new Text("{{Name}}")));
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        composite.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Single(visitor1.VisitedPlaceholders);
        Assert.Single(visitor2.VisitedPlaceholders);

        Assert.Equal(placeholder, visitor1.VisitedPlaceholders[0]);
        Assert.Equal(placeholder, visitor2.VisitedPlaceholders[0]);
    }

    [Fact]
    public void VisitParagraph_DispatchesToAllVisitors()
    {
        // Arrange
        MockVisitor visitor1 = new MockVisitor();
        MockVisitor visitor2 = new MockVisitor();

        CompositeVisitor composite = new CompositeVisitor(visitor1, visitor2);

        Paragraph paragraph = new Paragraph(new Run(new Text("Regular text")));
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        composite.VisitParagraph(paragraph, context);

        // Assert
        Assert.Single(visitor1.VisitedParagraphs);
        Assert.Single(visitor2.VisitedParagraphs);

        Assert.Same(paragraph, visitor1.VisitedParagraphs[0]);
        Assert.Same(paragraph, visitor2.VisitedParagraphs[0]);
    }

    [Fact]
    public void Constructor_WithEnumerable_CreatesComposite()
    {
        // Arrange
        MockVisitor visitor1 = new MockVisitor();
        MockVisitor visitor2 = new MockVisitor();
        List<ITemplateElementVisitor> visitors = new List<ITemplateElementVisitor> { visitor1, visitor2 };

        // Act
        CompositeVisitor composite = new CompositeVisitor(visitors);

        ConditionalBlock conditional = CreateTestConditionalBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        composite.VisitConditional(conditional, context);

        // Assert
        Assert.Single(visitor1.VisitedConditionals);
        Assert.Single(visitor2.VisitedConditionals);
    }

    [Fact]
    public void CompositeVisitor_SingleVisitor_Works()
    {
        // Arrange
        MockVisitor visitor = new MockVisitor();
        CompositeVisitor composite = new CompositeVisitor(visitor);

        ConditionalBlock conditional = CreateTestConditionalBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        composite.VisitConditional(conditional, context);

        // Assert
        Assert.Single(visitor.VisitedConditionals);
    }

    [Fact]
    public void CompositeVisitor_CallsVisitorsInOrder()
    {
        // Arrange
        OrderTrackingVisitor visitor1 = new OrderTrackingVisitor(1);
        OrderTrackingVisitor visitor2 = new OrderTrackingVisitor(2);
        OrderTrackingVisitor visitor3 = new OrderTrackingVisitor(3);

        CompositeVisitor composite = new CompositeVisitor(visitor1, visitor2, visitor3);

        ConditionalBlock conditional = CreateTestConditionalBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act
        composite.VisitConditional(conditional, context);

        // Assert
        Assert.Equal(3, OrderTrackingVisitor.CallOrder.Count);
        Assert.Equal(1, OrderTrackingVisitor.CallOrder[0]);
        Assert.Equal(2, OrderTrackingVisitor.CallOrder[1]);
        Assert.Equal(3, OrderTrackingVisitor.CallOrder[2]);
    }

    [Fact]
    public void CompositeVisitor_WithRealVisitors_WorksTogether()
    {
        // Arrange
        Body body = new Body();
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph ifContent = new Paragraph(new Run(new Text("Active")));
        Paragraph elseMarker = new Paragraph(new Run(new Text("{{else}}")));
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

        ConditionalVisitor conditionalVisitor = new ConditionalVisitor();
        MockVisitor mockVisitor = new MockVisitor();

        CompositeVisitor composite = new CompositeVisitor(conditionalVisitor, mockVisitor);

        Dictionary<string, object> data = new Dictionary<string, object> { ["IsActive"] = true };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        composite.VisitConditional(conditional, context);

        // Assert - ConditionalVisitor processed the block
        List<Paragraph> remaining = body.Elements<Paragraph>().ToList();
        Assert.Single(remaining);
        Assert.Equal("Active", remaining[0].InnerText);

        // MockVisitor also received the visit
        Assert.Single(mockVisitor.VisitedConditionals);
    }

    // Helper classes and methods

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

    private class OrderTrackingVisitor : ITemplateElementVisitor
    {
        public static List<int> CallOrder { get; } = new List<int>();
        private readonly int _id;

        public OrderTrackingVisitor(int id)
        {
            _id = id;
            CallOrder.Clear(); // Reset for each test
        }

        public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
        {
            CallOrder.Add(_id);
        }

        public void VisitLoop(LoopBlock loop, IEvaluationContext context)
        {
            CallOrder.Add(_id);
        }

        public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
        {
            CallOrder.Add(_id);
        }

        public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
        {
            CallOrder.Add(_id);
        }
    }
}
