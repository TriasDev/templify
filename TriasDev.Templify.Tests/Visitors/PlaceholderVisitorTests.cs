using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// Unit tests for PlaceholderVisitor.
/// Tests placeholder replacement with context-aware variable resolution.
/// </summary>
public sealed class PlaceholderVisitorTests
{
    [Fact]
    public void VisitPlaceholder_VariableFound_ReplacesPlaceholder()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Hello {{Name}}!")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 6,
            Length = 8
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World"
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Equal("Hello World!", paragraph.InnerText);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public void VisitPlaceholder_VariableNotFound_LeaveUnchanged()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Hello {{Name}}!")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 6,
            Length = 8
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
        };
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Equal("Hello {{Name}}!", paragraph.InnerText);
        Assert.Contains("Name", missingVariables);
    }

    [Fact]
    public void VisitPlaceholder_VariableNotFound_ReplaceWithEmpty()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Hello {{Name}}!")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 6,
            Length = 8
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
        };
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Equal("Hello !", paragraph.InnerText);
        Assert.Contains("Name", missingVariables);
    }

    [Fact]
    public void VisitPlaceholder_VariableNotFound_ThrowException()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Hello {{Name}}!")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 6,
            Length = 8
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        };
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            visitor.VisitPlaceholder(placeholder, paragraph, context));

        Assert.Contains("Name", exception.Message);
        Assert.Contains("Missing variable", exception.Message);
    }

    [Fact]
    public void VisitPlaceholder_NumericValue_ConvertsToString()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Count: {{Count}}")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Count",
            FullMatch = "{{Count}}",
            StartIndex = 7,
            Length = 9
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Count"] = 42
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Equal("Count: 42", paragraph.InnerText);
    }

    [Fact]
    public void VisitPlaceholder_DateValue_ConvertsToString()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Date: {{Date}}")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Date",
            FullMatch = "{{Date}}",
            StartIndex = 6,
            Length = 8
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        DateTime testDate = new DateTime(2025, 11, 9);
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Date"] = testDate
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Contains("2025", paragraph.InnerText);
        Assert.Contains("11", paragraph.InnerText);
        Assert.Contains("09", paragraph.InnerText);
    }

    [Fact]
    public void VisitPlaceholder_LoopContext_ResolvesLoopVariable()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Item: {{.}}")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = ".",
            FullMatch = "{{.}}",
            StartIndex = 6,
            Length = 5
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        // Create loop context
        LoopContext loopContext = new LoopContext("TestItem", 0, 1, "Items");
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        LoopEvaluationContext context = new LoopEvaluationContext(loopContext, globalContext);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Equal("Item: TestItem", paragraph.InnerText);
    }

    [Fact]
    public void VisitPlaceholder_LoopMetadata_ResolvesMetadataVariable()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Index: {{@index}}")));
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "@index",
            FullMatch = "{{@index}}",
            StartIndex = 7,
            Length = 10
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        // Create loop context (index 2 of 5)
        LoopContext loopContext = new LoopContext("Item", 2, 5, "Items");
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        LoopEvaluationContext context = new LoopEvaluationContext(loopContext, globalContext);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Equal("Index: 2", paragraph.InnerText);
    }

    [Fact]
    public void VisitConditional_DoesNothing()
    {
        // Arrange
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        ConditionalBlock conditional = CreateTestConditionalBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitConditional(conditional, context);

        // Assert - no exception, no-op completed
        Assert.NotNull(conditional);
    }

    [Fact]
    public void VisitLoop_DoesNothing()
    {
        // Arrange
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        LoopBlock loop = CreateTestLoopBlock();
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitLoop(loop, context);

        // Assert - no exception, no-op completed
        Assert.NotNull(loop);
    }

    [Fact]
    public void VisitParagraph_DoesNothing()
    {
        // Arrange
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables);

        Paragraph paragraph = new Paragraph(new Run(new Text("Regular text")));
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitParagraph(paragraph, context);

        // Assert - paragraph unchanged
        Assert.Equal("Regular text", paragraph.InnerText);
    }

    // Helper methods

    private static ConditionalBlock CreateTestConditionalBlock()
    {
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));
        List<DocumentFormat.OpenXml.OpenXmlElement> ifContent = new List<DocumentFormat.OpenXml.OpenXmlElement>
        {
            new Paragraph(new Run(new Text("Active")))
        };

        return new ConditionalBlock(
            conditionExpression: "IsActive",
            ifContentElements: ifContent,
            elseContentElements: new List<DocumentFormat.OpenXml.OpenXmlElement>(),
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
        List<DocumentFormat.OpenXml.OpenXmlElement> content = new List<DocumentFormat.OpenXml.OpenXmlElement>
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
