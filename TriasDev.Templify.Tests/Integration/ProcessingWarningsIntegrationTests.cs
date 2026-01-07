// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for processing warnings feature.
/// Tests that warnings are collected during template processing for missing variables,
/// null/missing loop collections, etc.
/// </summary>
public sealed class ProcessingWarningsIntegrationTests
{
    [Fact]
    public void ProcessTemplate_MissingVariable_CollectsWarning()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{MissingName}}!");

        Dictionary<string, object> data = new Dictionary<string, object>();

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.HasWarnings);
        Assert.Single(result.Warnings);

        ProcessingWarning warning = result.Warnings[0];
        Assert.Equal(ProcessingWarningType.MissingVariable, warning.Type);
        Assert.Equal("MissingName", warning.VariableName);
        Assert.Contains("MissingName", warning.Message);
    }

    [Fact]
    public void ProcessTemplate_MultipleMissingVariables_CollectsAllWarnings()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Missing1}} and {{Missing2}} and {{Missing3}}");

        Dictionary<string, object> data = new Dictionary<string, object>();

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Warnings.Count);
        Assert.All(result.Warnings, w => Assert.Equal(ProcessingWarningType.MissingVariable, w.Type));
    }

    [Fact]
    public void ProcessTemplate_MissingLoopCollection_CollectsWarning()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach MissingItems}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>();

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.HasWarnings);
        Assert.Single(result.Warnings);

        ProcessingWarning warning = result.Warnings[0];
        Assert.Equal(ProcessingWarningType.MissingLoopCollection, warning.Type);
        Assert.Equal("MissingItems", warning.VariableName);
        Assert.Contains("loop: MissingItems", warning.Context);
    }

    [Fact]
    public void ProcessTemplate_NullLoopCollection_CollectsWarning()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach NullItems}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object?> data = new Dictionary<string, object?>
        {
            ["NullItems"] = null
        };

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data!);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.HasWarnings);
        Assert.Single(result.Warnings);

        ProcessingWarning warning = result.Warnings[0];
        Assert.Equal(ProcessingWarningType.NullLoopCollection, warning.Type);
        Assert.Equal("NullItems", warning.VariableName);
        Assert.Contains("null", warning.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessTemplate_EmptyCollection_NoWarning()
    {
        // Arrange - empty collection should NOT produce a warning (by design)
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach EmptyItems}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["EmptyItems"] = new List<string>() // Empty, but valid
        };

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.HasWarnings);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ProcessTemplate_ValidData_NoWarnings()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World",
            ["Items"] = new List<string> { "A", "B", "C" }
        };

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.HasWarnings);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ProcessTemplate_MixedWarnings_CollectsAll()
    {
        // Arrange - multiple warning types in one template
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{MissingName}}!");
        builder.AddParagraph("{{#foreach MissingItems}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{#foreach NullItems}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object?> data = new Dictionary<string, object?>
        {
            ["NullItems"] = null
        };

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data!);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.HasWarnings);
        Assert.Equal(3, result.Warnings.Count);

        // Verify we have different warning types
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.MissingVariable);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.MissingLoopCollection);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.NullLoopCollection);
    }

    [Fact]
    public void ProcessingWarning_ToString_ContainsUsefulInfo()
    {
        // Arrange
        ProcessingWarning warning = ProcessingWarning.MissingVariable("TestVar");

        // Act
        string str = warning.ToString();

        // Assert
        Assert.Contains("MissingVariable", str);
        Assert.Contains("TestVar", str);
    }

    [Fact]
    public void ProcessTemplate_NestedMissingVariable_CollectsWarning()
    {
        // Arrange - missing nested path like Customer.Name
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Customer.Name}}");

        Dictionary<string, object> data = new Dictionary<string, object>();

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        // Act
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.HasWarnings);
        Assert.Single(result.Warnings);
        Assert.Equal("Customer.Name", result.Warnings[0].VariableName);
    }

    #region Warning Report Tests

    [Fact]
    public void GetWarningReport_WithWarnings_ReturnsValidDocx()
    {
        // Arrange - create a result with warnings
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{MissingName}}!");
        builder.AddParagraph("{{#foreach MissingItems}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>();

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Act
        using MemoryStream reportStream = result.GetWarningReport();

        // Assert - verify it's a valid docx
        Assert.True(reportStream.Length > 0);
        reportStream.Position = 0;

        using WordprocessingDocument doc = WordprocessingDocument.Open(reportStream, false);
        Body? body = doc.MainDocumentPart?.Document.Body;
        Assert.NotNull(body);

        string text = body.InnerText;
        Assert.Contains("Warning Report", text);
        Assert.Contains("2", text); // Total 2 warnings
    }

    [Fact]
    public void GetWarningReport_NoWarnings_ReturnsEmptyReport()
    {
        // Arrange - create a result with no warnings
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World"
        };

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Act
        using MemoryStream reportStream = result.GetWarningReport();

        // Assert - verify it's a valid docx with 0 warnings
        Assert.True(reportStream.Length > 0);
        reportStream.Position = 0;

        using WordprocessingDocument doc = WordprocessingDocument.Open(reportStream, false);
        Body? body = doc.MainDocumentPart?.Document.Body;
        Assert.NotNull(body);

        string text = body.InnerText;
        Assert.Contains("Warning Report", text);
        Assert.Contains("Total Warnings: 0", text);
    }

    [Fact]
    public void GetWarningReportBytes_WithWarnings_ReturnsByteArray()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{MissingVar}}!");

        Dictionary<string, object> data = new Dictionary<string, object>();

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Act
        byte[] reportBytes = result.GetWarningReportBytes();

        // Assert
        Assert.True(reportBytes.Length > 0);

        // Verify it's a valid docx by loading it
        using MemoryStream stream = new MemoryStream(reportBytes);
        using WordprocessingDocument doc = WordprocessingDocument.Open(stream, false);
        Assert.NotNull(doc.MainDocumentPart?.Document.Body);
    }

    [Fact]
    public void GetWarningReport_MixedWarnings_ContainsAllSections()
    {
        // Arrange - create warnings of all types
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{MissingVar1}}");
        builder.AddParagraph("{{MissingVar2}}");
        builder.AddParagraph("{{#foreach MissingCollection}}{{.}}{{/foreach}}");
        builder.AddParagraph("{{#foreach NullCollection}}{{.}}{{/foreach}}");

        Dictionary<string, object?> data = new Dictionary<string, object?>
        {
            ["NullCollection"] = null
        };

        using MemoryStream templateStream = builder.ToStream();
        using MemoryStream outputStream = new MemoryStream();

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data!);

        // Act
        using MemoryStream reportStream = result.GetWarningReport();

        // Assert
        reportStream.Position = 0;
        using WordprocessingDocument doc = WordprocessingDocument.Open(reportStream, false);
        Body? body = doc.MainDocumentPart?.Document.Body;
        Assert.NotNull(body);

        string text = body.InnerText;

        // Should contain all sections
        Assert.Contains("Missing Variables", text);
        Assert.Contains("Missing Loop Collections", text);
        Assert.Contains("Null Loop Collections", text);

        // Should contain the specific variable names
        Assert.Contains("MissingVar1", text);
        Assert.Contains("MissingVar2", text);
        Assert.Contains("MissingCollection", text);
        Assert.Contains("NullCollection", text);

        // Should have correct counts
        Assert.Contains("Total Warnings: 4", text);
    }

    #endregion
}
