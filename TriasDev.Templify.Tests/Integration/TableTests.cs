using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for table placeholder replacement and formatting.
/// These tests create actual Word documents with tables, process them, and verify the output.
/// </summary>
public sealed class TableTests
{
    [Fact]
    public void ProcessTemplate_SimplePlaceholderInTable_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(2, 3, (row, col) =>
        {
            if (row == 0)
            {
                // Header row
                return col switch
                {
                    0 => "Product",
                    1 => "Price",
                    2 => "Status",
                    _ => ""
                };
            }
            else
            {
                // Data row with placeholders
                return col switch
                {
                    0 => "{{ProductName}}",
                    1 => "{{Price}}",
                    2 => "{{Status}}",
                    _ => ""
                };
            }
        });

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["ProductName"] = "Software License",
            ["Price"] = 999.00m,
            ["Status"] = "Active"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetTableCount());

        // Verify header row unchanged
        Assert.Equal("Product", verifier.GetTableCellText(0, 0, 0));
        Assert.Equal("Price", verifier.GetTableCellText(0, 0, 1));
        Assert.Equal("Status", verifier.GetTableCellText(0, 0, 2));

        // Verify data row replaced
        Assert.Equal("Software License", verifier.GetTableCellText(0, 1, 0));
        Assert.Contains("999", verifier.GetTableCellText(0, 1, 1));
        Assert.Equal("Active", verifier.GetTableCellText(0, 1, 2));
    }

    [Fact]
    public void ProcessTemplate_MultipleRowsWithPlaceholders_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(3, 2, (row, col) =>
        {
            if (row == 0)
            {
                return col == 0 ? "Name" : "Value";
            }
            else if (row == 1)
            {
                return col == 0 ? "{{Name1}}" : "{{Value1}}";
            }
            else
            {
                return col == 0 ? "{{Name2}}" : "{{Value2}}";
            }
        });

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name1"] = "Company",
            ["Value1"] = "TriasDev GmbH & Co. KG",
            ["Name2"] = "Location",
            ["Value2"] = "Munich"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Row 1
        Assert.Equal("Company", verifier.GetTableCellText(0, 1, 0));
        Assert.Equal("TriasDev GmbH & Co. KG", verifier.GetTableCellText(0, 1, 1));

        // Row 2
        Assert.Equal("Location", verifier.GetTableCellText(0, 2, 0));
        Assert.Equal("Munich", verifier.GetTableCellText(0, 2, 1));
    }

    [Fact]
    public void ProcessTemplate_TableWithFormatting_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        RunProperties italicFormatting = DocumentBuilder.CreateFormatting(italic: true);

        builder.AddTableWithFormatting(2, 2, (row, col) =>
        {
            if (row == 0 && col == 0)
            {
                return ("{{Title}}", boldFormatting);
            }
            else if (row == 0 && col == 1)
            {
                return ("{{Subtitle}}", italicFormatting);
            }
            else if (row == 1 && col == 0)
            {
                return ("{{Content1}}", null);
            }
            else
            {
                return ("{{Content2}}", null);
            }
        });

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "Bold Title",
            ["Subtitle"] = "Italic Subtitle",
            ["Content1"] = "Normal Text 1",
            ["Content2"] = "Normal Text 2"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Verify content
        Assert.Equal("Bold Title", verifier.GetTableCellText(0, 0, 0));
        Assert.Equal("Italic Subtitle", verifier.GetTableCellText(0, 0, 1));

        // Verify formatting preserved
        RunProperties? boldProps = verifier.GetTableCellRunProperties(0, 0, 0, 0);
        DocumentVerifier.VerifyFormatting(boldProps, expectedBold: true);

        RunProperties? italicProps = verifier.GetTableCellRunProperties(0, 0, 1, 0);
        DocumentVerifier.VerifyFormatting(italicProps, expectedItalic: true);
    }

    [Fact]
    public void ProcessTemplate_NestedStructuresInTable_ResolvesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(2, 2, (row, col) =>
        {
            if (row == 0)
            {
                return col == 0 ? "Customer" : "City";
            }
            else
            {
                return col == 0 ? "{{Customer.Name}}" : "{{Customer.Address.City}}";
            }
        });

        MemoryStream templateStream = builder.ToStream();

        var customerData = new
        {
            Name = "TriasDev GmbH & Co. KG",
            Address = new
            {
                City = "Munich",
                PostalCode = "80331"
            }
        };

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = customerData
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("TriasDev GmbH & Co. KG", verifier.GetTableCellText(0, 1, 0));
        Assert.Equal("Munich", verifier.GetTableCellText(0, 1, 1));
    }

    [Fact]
    public void ProcessTemplate_MultipleTablesInDocument_ReplacesInAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        // First table
        builder.AddTable(1, 2, (row, col) =>
            col == 0 ? "{{Table1Col1}}" : "{{Table1Col2}}");

        builder.AddParagraph("Between tables");

        // Second table
        builder.AddTable(1, 2, (row, col) =>
            col == 0 ? "{{Table2Col1}}" : "{{Table2Col2}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Table1Col1"] = "First Table A",
            ["Table1Col2"] = "First Table B",
            ["Table2Col1"] = "Second Table A",
            ["Table2Col2"] = "Second Table B"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetTableCount());

        // First table
        Assert.Equal("First Table A", verifier.GetTableCellText(0, 0, 0));
        Assert.Equal("First Table B", verifier.GetTableCellText(0, 0, 1));

        // Second table
        Assert.Equal("Second Table A", verifier.GetTableCellText(1, 0, 0));
        Assert.Equal("Second Table B", verifier.GetTableCellText(1, 0, 1));
    }

    [Fact]
    public void ProcessTemplate_TableWithMissingVariable_HandlesAccordingToOptions()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(1, 2, (row, col) =>
            col == 0 ? "{{ExistingVar}}" : "{{MissingVar}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["ExistingVar"] = "Present"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount); // Only ExistingVar was replaced
        Assert.Single(result.MissingVariables);
        Assert.Contains("MissingVar", result.MissingVariables);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Present", verifier.GetTableCellText(0, 0, 0));
        Assert.Equal("{{MissingVar}}", verifier.GetTableCellText(0, 0, 1));
    }
}
