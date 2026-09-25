// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Visitors;

using static TriasDev.Templify.Tests.Helpers.TestBlocks;

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
        PlaceholderToken placeholder = new PlaceholderToken
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 6,
            Length = 8
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderToken placeholder = new PlaceholderToken
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
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderToken placeholder = new PlaceholderToken
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
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderToken placeholder = new PlaceholderToken
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
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act & Assert
        MissingVariableException exception = Assert.Throws<MissingVariableException>(() =>
            visitor.VisitPlaceholder(placeholder, paragraph, context));

        Assert.Contains("Name", exception.Message);
        Assert.Contains("Missing variable", exception.Message);
    }

    [Fact]
    public void VisitPlaceholder_NumericValue_ConvertsToString()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Count: {{Count}}")));
        PlaceholderToken placeholder = new PlaceholderToken
        {
            VariableName = "Count",
            FullMatch = "{{Count}}",
            StartIndex = 7,
            Length = 9
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderToken placeholder = new PlaceholderToken
        {
            VariableName = "Date",
            FullMatch = "{{Date}}",
            StartIndex = 6,
            Length = 8
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture };
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

        DateTime testDate = new DateTime(2025, 11, 9);
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Date"] = testDate
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        visitor.VisitPlaceholder(placeholder, paragraph, context);

        // Assert
        Assert.Equal($"Date: {testDate.ToString(CultureInfo.InvariantCulture)}", paragraph.InnerText);
    }

    [Fact]
    public void VisitPlaceholder_LoopContext_ResolvesLoopVariable()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Item: {{.}}")));
        PlaceholderToken placeholder = new PlaceholderToken
        {
            VariableName = ".",
            FullMatch = "{{.}}",
            StartIndex = 6,
            Length = 5
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderToken placeholder = new PlaceholderToken
        {
            VariableName = "@index",
            FullMatch = "{{@index}}",
            StartIndex = 7,
            Length = 10
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        HashSet<string> missingVariables = new HashSet<string>();
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

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
        PlaceholderVisitor visitor = new PlaceholderVisitor(options, missingVariables, new WarningCollector());

        Paragraph paragraph = new Paragraph(new Run(new Text("Regular text")));
        GlobalEvaluationContext context = new GlobalEvaluationContext(new Dictionary<string, object>());

        // Act (should not throw and should not modify anything)
        visitor.VisitParagraph(paragraph, context);

        // Assert - paragraph unchanged
        Assert.Equal("Regular text", paragraph.InnerText);
    }
}
