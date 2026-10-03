// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
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
/// Integration tests for loop functionality ({{#foreach}}...{{/foreach}}).
/// These tests create actual Word documents, process them, and verify the output.
/// </summary>
public sealed class LoopTests
{
    // Test data classes
    private class LineItem
    {
        public int Position { get; set; }
        public string Product { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    private class Order
    {
        public string OrderId { get; set; } = "";
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    }

    private class OrderItem
    {
        public string Product { get; set; } = "";
        public int Quantity { get; set; }
    }

    [Fact]
    public void ProcessTemplate_SimpleLoop_RepeatsContentForEachItem()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Line Items:");
        builder.AddParagraph("{{#foreach LineItems}}");
        builder.AddParagraph("{{Position}}. {{Product}} - Qty: {{Quantity}} @ {{UnitPrice}} EUR = {{Total}} EUR");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("End of list");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["LineItems"] = new List<LineItem>
            {
                new LineItem { Position = 1, Product = "Software License", Quantity = 5, UnitPrice = 499.00m, Total = 2495.00m },
                new LineItem { Position = 2, Product = "Support Package", Quantity = 5, UnitPrice = 99.00m, Total = 495.00m },
                new LineItem { Position = 3, Product = "Training", Quantity = 2, UnitPrice = 250.00m, Total = 500.00m }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(5, paragraphs.Count); // "Line Items:" + 3 items + "End of list"
        Assert.Equal("Line Items:", paragraphs[0]);
        Assert.Equal("1. Software License - Qty: 5 @ 499.00 EUR = 2495.00 EUR", paragraphs[1]);
        Assert.Equal("2. Support Package - Qty: 5 @ 99.00 EUR = 495.00 EUR", paragraphs[2]);
        Assert.Equal("3. Training - Qty: 2 @ 250.00 EUR = 500.00 EUR", paragraphs[3]);
        Assert.Equal("End of list", paragraphs[4]);
    }

    [Fact]
    public void ProcessTemplate_PrimitiveValueLoop_UsesDotNotation()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Items:");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Item One", "Item Two", "Item Three" }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(4, paragraphs.Count); // "Items:" + 3 items
        Assert.Equal("Items:", paragraphs[0]);
        Assert.Equal("- Item One", paragraphs[1]);
        Assert.Equal("- Item Two", paragraphs[2]);
        Assert.Equal("- Item Three", paragraphs[3]);
    }

    [Fact]
    public void ProcessTemplate_PrimitiveValueLoop_UsesThisKeyword()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Numbers:");
        builder.AddParagraph("{{#foreach Numbers}}");
        builder.AddParagraph("Value: {{this}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Numbers"] = new List<int> { 10, 20, 30 }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(4, paragraphs.Count);
        Assert.Equal("Numbers:", paragraphs[0]);
        Assert.Equal("Value: 10", paragraphs[1]);
        Assert.Equal("Value: 20", paragraphs[2]);
        Assert.Equal("Value: 30", paragraphs[3]);
    }

    [Fact]
    public void ProcessTemplate_LoopMetadata_AccessesIndexAndFlags()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Item {{@index}}: {{.}} (First: {{@first}}, Last: {{@last}}, Count: {{@count}})");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Alpha", "Beta", "Gamma" }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(3, paragraphs.Count);

        // First item
        string item1 = paragraphs[0];
        Assert.Contains("Item 0: Alpha", item1);
        Assert.Contains("First: True", item1);
        Assert.Contains("Last: False", item1);
        Assert.Contains("Count: 3", item1);

        // Second item
        string item2 = paragraphs[1];
        Assert.Contains("Item 1: Beta", item2);
        Assert.Contains("First: False", item2);
        Assert.Contains("Last: False", item2);

        // Third item
        string item3 = paragraphs[2];
        Assert.Contains("Item 2: Gamma", item3);
        Assert.Contains("First: False", item3);
        Assert.Contains("Last: True", item3);
    }

    [Fact]
    public void ProcessTemplate_NestedLoops_ProcessesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Orders}}");
        builder.AddParagraph("Order: {{OrderId}}");
        builder.AddParagraph("  Items:");
        builder.AddParagraph("  {{#foreach Items}}");
        builder.AddParagraph("    - {{Product}} (Qty: {{Quantity}})");
        builder.AddParagraph("  {{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Orders"] = new List<Order>
            {
                new Order
                {
                    OrderId = "ORD-001",
                    Items = new List<OrderItem>
                    {
                        new OrderItem { Product = "Product A", Quantity = 2 },
                        new OrderItem { Product = "Product B", Quantity = 1 }
                    }
                },
                new Order
                {
                    OrderId = "ORD-002",
                    Items = new List<OrderItem>
                    {
                        new OrderItem { Product = "Product C", Quantity = 3 }
                    }
                }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Order 1
        Assert.Contains(paragraphs, p => p.Contains("Order: ORD-001"));
        Assert.Contains(paragraphs, p => p.Contains("- Product A (Qty: 2)"));
        Assert.Contains(paragraphs, p => p.Contains("- Product B (Qty: 1)"));

        // Order 2
        Assert.Contains(paragraphs, p => p.Contains("Order: ORD-002"));
        Assert.Contains(paragraphs, p => p.Contains("- Product C (Qty: 3)"));
    }

    [Fact]
    public void ProcessTemplate_EmptyCollection_RemovesLoopBlock()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Before loop");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("After loop");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string>() // Empty collection
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("Before loop", paragraphs[0]);
        Assert.Equal("After loop", paragraphs[1]);
    }

    [Fact]
    public void ProcessTemplate_TableRowLoop_RepeatsRows()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(4, 3, (row, col) =>
        {
            if (row == 0)
            {
                // Header row
                return col switch
                {
                    0 => "Position",
                    1 => "Product",
                    2 => "Price",
                    _ => ""
                };
            }
            else if (row == 1)
            {
                // Loop start marker
                return col == 0 ? "{{#foreach LineItems}}" : "";
            }
            else if (row == 2)
            {
                // Loop content
                return col switch
                {
                    0 => "{{Position}}",
                    1 => "{{Product}}",
                    2 => "{{UnitPrice}}",
                    _ => ""
                };
            }
            else // row == 3
            {
                // Loop end marker
                return col == 0 ? "{{/foreach}}" : "";
            }
        });

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["LineItems"] = new List<LineItem>
            {
                new LineItem { Position = 1, Product = "Software License", UnitPrice = 499.00m },
                new LineItem { Position = 2, Product = "Support Package", UnitPrice = 99.00m }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<List<string>> tableCells = verifier.GetTableCellTexts(0);

        // Header row should remain
        Assert.Equal("Position", tableCells[0][0]);
        Assert.Equal("Product", tableCells[0][1]);
        Assert.Equal("Price", tableCells[0][2]);

        // First data row
        Assert.Equal("1", tableCells[1][0]);
        Assert.Equal("Software License", tableCells[1][1]);
        Assert.Equal("499.00", tableCells[1][2]);

        // Second data row
        Assert.Equal("2", tableCells[2][0]);
        Assert.Equal("Support Package", tableCells[2][1]);
        Assert.Equal("99.00", tableCells[2][2]);
    }

    [Fact]
    public void ProcessTemplate_LoopAccessesRootData_BesidesLoopVariables()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Invoice: {{InvoiceNumber}}");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{.}} (Invoice: {{InvoiceNumber}})");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["InvoiceNumber"] = "INV-2025-001",
            ["Items"] = new List<string> { "Item A", "Item B" }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal("Invoice: INV-2025-001", paragraphs[0]);
        Assert.Equal("- Item A (Invoice: INV-2025-001)", paragraphs[1]);
        Assert.Equal("- Item B (Invoice: INV-2025-001)", paragraphs[2]);
    }

    [Fact]
    public void ProcessTemplate_NestedLoopWithRelativePath_ProcessesCorrectly()
    {
        // Arrange
        // This test verifies that nested loops can use relative collection paths
        // Variables already support relative paths (e.g., {{Name}} resolves to current item's Name)
        // Loop collections should work the same way (e.g., {{#foreach Items}} resolves to current item's Items)
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Categories:");
        builder.AddParagraph("{{#foreach Categories}}");
        builder.AddParagraph("  Category: {{Name}}");
        builder.AddParagraph("  Items:");
        builder.AddParagraph("  {{#foreach Items}}"); // ← Relative path! Should resolve to Categories[].Items
        builder.AddParagraph("    - {{Title}}");
        builder.AddParagraph("  {{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "Category A",
                    ["Items"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { ["Title"] = "Item 1" },
                        new Dictionary<string, object> { ["Title"] = "Item 2" }
                    }
                },
                new Dictionary<string, object>
                {
                    ["Name"] = "Category B",
                    ["Items"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { ["Title"] = "Item 3" }
                    }
                }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess, $"Processing should succeed. Error: {result.ErrorMessage}");

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Should have: "Categories:" + Category A header + 2 items + "Items:" + Category B header + 1 item + "Items:"
        Assert.Contains(paragraphs, p => p.Contains("Category A"));
        Assert.Contains(paragraphs, p => p.Contains("Category B"));

        // Most importantly: Item titles should appear (proving nested loop was processed)
        Assert.Contains(paragraphs, p => p.Contains("Item 1"));
        Assert.Contains(paragraphs, p => p.Contains("Item 2"));
        Assert.Contains(paragraphs, p => p.Contains("Item 3"));

        // Loop markers should NOT appear in output (proving loops were processed, not left as-is)
        string fullText = string.Join(" ", paragraphs);
        Assert.DoesNotContain("{{#foreach Items}}", fullText);
        Assert.DoesNotContain("{{/foreach}}", fullText);
    }

    [Fact]
    public void ProcessTemplate_NestedLoopWithSplitTextRuns_ProcessesCorrectly()
    {
        // Arrange
        // This test reproduces the issue where loop markers are split across multiple runs
        // in the Word document, which can happen after editing or formatting changes
        using MemoryStream templateStream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(templateStream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Add outer loop
            Paragraph p1 = body.AppendChild(new Paragraph());
            p1.AppendChild(new Run(new Text("{{#foreach Categories}}")));

            Paragraph p2 = body.AppendChild(new Paragraph());
            p2.AppendChild(new Run(new Text("Category: {{Name}}")));

            // Add inner loop with SPLIT text across multiple runs (simulating Word editing behavior)
            Paragraph p3 = body.AppendChild(new Paragraph());
            // Split "{{#foreach controls}}" across 3 runs: "{{#foreach ", "con", "trols}}"
            p3.AppendChild(new Run(new Text("{{#foreach ")));
            p3.AppendChild(new Run(new Text("con")));
            p3.AppendChild(new Run(new Text("trols}}")));

            Paragraph p4 = body.AppendChild(new Paragraph());
            p4.AppendChild(new Run(new Text("  Control: {{name}}")));

            // Split end marker too: "{{/for", "each}}"
            Paragraph p5 = body.AppendChild(new Paragraph());
            p5.AppendChild(new Run(new Text("{{/for")));
            p5.AppendChild(new Run(new Text("each}}")));

            Paragraph p6 = body.AppendChild(new Paragraph());
            p6.AppendChild(new Run(new Text("{{/foreach}}")));

            mainPart.Document.Save();
        }

        templateStream.Position = 0;

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "Category A",
                    ["controls"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { ["name"] = "Control 1" },
                        new Dictionary<string, object> { ["name"] = "Control 2" }
                    }
                }
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess, $"Processing should succeed. Error: {result.ErrorMessage}");

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Should have processed both loops
        Assert.Contains(paragraphs, p => p.Contains("Category A"));
        Assert.Contains(paragraphs, p => p.Contains("Control 1"));
        Assert.Contains(paragraphs, p => p.Contains("Control 2"));

        // Loop markers should NOT appear in output
        string fullText = string.Join(" ", paragraphs);
        Assert.DoesNotContain("{{#foreach", fullText);
        Assert.DoesNotContain("{{/foreach}}", fullText);
    }

    [Fact]
    public void ProcessTemplate_NestedLoopInsideTableCell_ProcessesCorrectly()
    {
        // Arrange
        // This test checks if nested loops inside table cells are detected and processed
        // This might be the issue with the production template
        using MemoryStream templateStream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(templateStream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Outer loop
            body.AppendChild(new Paragraph(new Run(new Text("{{#foreach Categories}}"))));
            body.AppendChild(new Paragraph(new Run(new Text("Category: {{Name}}"))));

            // Create a table with nested loop INSIDE a cell
            Table table = new Table();
            TableRow row = new TableRow();
            TableCell cell = new TableCell();

            // Add nested loop markers inside the table cell
            cell.AppendChild(new Paragraph(new Run(new Text("{{#foreach controls}}"))));
            cell.AppendChild(new Paragraph(new Run(new Text("Control: {{name}}"))));
            cell.AppendChild(new Paragraph(new Run(new Text("{{/foreach}}"))));

            row.AppendChild(cell);
            table.AppendChild(row);
            body.AppendChild(table);

            // Close outer loop
            body.AppendChild(new Paragraph(new Run(new Text("{{/foreach}}"))));

            mainPart.Document.Save();
        }

        templateStream.Position = 0;

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "Category A",
                    ["controls"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { ["name"] = "Control 1" },
                        new Dictionary<string, object> { ["name"] = "Control 2" }
                    }
                }
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess, $"Processing should succeed. Error: {result.ErrorMessage}");

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Also get text from table cells
        List<string> allTexts = new List<string>(paragraphs);
        int tableCount = verifier.GetTableCount();
        for (int i = 0; i < tableCount; i++)
        {
            List<List<string>> tableCells = verifier.GetTableCellTexts(i);
            foreach (List<string> row in tableCells)
            {
                allTexts.AddRange(row);
            }
        }

        // Debug: Print what we got
        string debugOutput = string.Join(" | ", allTexts);
        System.Console.WriteLine($"Output (all texts): {debugOutput}");

        // Should have processed both loops
        Assert.Contains(allTexts, t => t.Contains("Category A"));
        Assert.Contains(allTexts, t => t.Contains("Control 1"));
        Assert.Contains(allTexts, t => t.Contains("Control 2"));

        // Loop markers should NOT appear in output
        string fullText = string.Join(" ", allTexts);
        Assert.DoesNotContain("{{#foreach", fullText);
        Assert.DoesNotContain("{{/foreach}}", fullText);
    }

    [Fact]
    public void ProcessTemplate_NestedLoopAcrossTableRowsInsideOuterLoop_ProcessesCorrectly()
    {
        // Arrange
        // This test reproduces the production issue where:
        // - Outer loop clones content containing a table
        // - Nested loop markers are in separate TABLE ROWS (table row loop pattern)
        // - After cloning, nested table row loops should be detected and processed
        // - Currently FAILS: nested loop markers remain unprocessed (99 markers in production)
        using MemoryStream templateStream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(templateStream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Outer loop start
            body.AppendChild(new Paragraph(new Run(new Text("{{#foreach Categories}}"))));
            body.AppendChild(new Paragraph(new Run(new Text("Category: {{Name}}"))));

            // Create a table with nested loop markers in SEPARATE ROWS (table row loop)
            Table table = new Table();

            // Row 1: Nested loop start marker
            TableRow row1 = new TableRow();
            TableCell cell1 = new TableCell();
            cell1.AppendChild(new Paragraph(new Run(new Text("{{#foreach controls}}"))));
            row1.AppendChild(cell1);
            table.AppendChild(row1);

            // Row 2: Loop content
            TableRow row2 = new TableRow();
            TableCell cell2 = new TableCell();
            cell2.AppendChild(new Paragraph(new Run(new Text("Control: {{name}}"))));
            row2.AppendChild(cell2);
            table.AppendChild(row2);

            // Row 3: Nested loop end marker
            TableRow row3 = new TableRow();
            TableCell cell3 = new TableCell();
            cell3.AppendChild(new Paragraph(new Run(new Text("{{/foreach}}"))));
            row3.AppendChild(cell3);
            table.AppendChild(row3);

            body.AppendChild(table);

            // Outer loop end
            body.AppendChild(new Paragraph(new Run(new Text("{{/foreach}}"))));

            mainPart.Document.Save();
        }

        templateStream.Position = 0;

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "Category A",
                    ["controls"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { ["name"] = "Control 1" },
                        new Dictionary<string, object> { ["name"] = "Control 2" }
                    }
                },
                new Dictionary<string, object>
                {
                    ["Name"] = "Category B",
                    ["controls"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { ["name"] = "Control 3" }
                    }
                }
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess, $"Processing should succeed. Error: {result.ErrorMessage}");

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Also get text from table cells
        List<string> allTexts = new List<string>(paragraphs);
        int tableCount = verifier.GetTableCount();
        for (int i = 0; i < tableCount; i++)
        {
            List<List<string>> tableCells = verifier.GetTableCellTexts(i);
            foreach (List<string> row in tableCells)
            {
                allTexts.AddRange(row);
            }
        }

        // Debug output
        string debugOutput = string.Join(" | ", allTexts);
        System.Console.WriteLine($"Output (nested table row loop test): {debugOutput}");

        // Should have processed both loops
        Assert.Contains(allTexts, t => t.Contains("Category A"));
        Assert.Contains(allTexts, t => t.Contains("Category B"));
        Assert.Contains(allTexts, t => t.Contains("Control 1"));
        Assert.Contains(allTexts, t => t.Contains("Control 2"));
        Assert.Contains(allTexts, t => t.Contains("Control 3"));

        // Loop markers should NOT appear in output
        string fullText = string.Join(" ", allTexts);
        Assert.DoesNotContain("{{#foreach", fullText);
        Assert.DoesNotContain("{{/foreach}}", fullText);
    }

    [Fact]
    public void ProcessTemplate_ThreeLevelsOfNestedLoops_ProcessesCorrectly()
    {
        // Arrange
        // This test reproduces the production issue with 3 levels of nesting:
        // - Level 1: process.assessments.organisations.items
        // - Level 2: assessment.categories (inside level 1)
        // - Level 3: controls (inside level 2)
        // Structure matches: [388] organisations → [392] categories → [394] controls
        DocumentBuilder builder = new DocumentBuilder();

        // Level 1 start
        builder.AddParagraph("{{#foreach Organisations}}");
        builder.AddParagraph("Organisation: {{orgName}}");

        // Level 2 start (nested in level 1)
        builder.AddParagraph("{{#foreach categories}}");
        builder.AddParagraph("Category: {{catName}}");

        // Level 3 start (nested in level 2)
        builder.AddParagraph("{{#foreach controls}}");
        builder.AddParagraph("Control: {{controlName}}");
        builder.AddParagraph("{{/foreach}}");  // Close level 3

        builder.AddParagraph("{{/foreach}}");  // Close level 2
        builder.AddParagraph("{{/foreach}}");  // Close level 1

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Organisations"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["orgName"] = "Org A",
                    ["categories"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["catName"] = "Category 1",
                            ["controls"] = new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object> { ["controlName"] = "Control 1-1" },
                                new Dictionary<string, object> { ["controlName"] = "Control 1-2" }
                            }
                        }
                    }
                },
                new Dictionary<string, object>
                {
                    ["orgName"] = "Org B",
                    ["categories"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["catName"] = "Category 2",
                            ["controls"] = new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object> { ["controlName"] = "Control 2-1" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess, $"Processing should succeed. Error: {result.ErrorMessage}");

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Debug output
        string debugOutput = string.Join(" | ", paragraphs);
        System.Console.WriteLine($"Output (3-level nested): {debugOutput}");

        // Should have processed all 3 levels of loops
        Assert.Contains(paragraphs, p => p.Contains("Org A"));
        Assert.Contains(paragraphs, p => p.Contains("Org B"));
        Assert.Contains(paragraphs, p => p.Contains("Category 1"));
        Assert.Contains(paragraphs, p => p.Contains("Category 2"));
        Assert.Contains(paragraphs, p => p.Contains("Control 1-1"));
        Assert.Contains(paragraphs, p => p.Contains("Control 1-2"));
        Assert.Contains(paragraphs, p => p.Contains("Control 2-1"));

        // Loop markers should NOT appear in output
        string fullText = string.Join(" ", paragraphs);
        Assert.DoesNotContain("{{#foreach", fullText);
        Assert.DoesNotContain("{{/foreach}}", fullText);
    }

    // TODO: Same-line loops require special processing logic beyond loop detection
    // These tests are commented out until that functionality is implemented

    /*
    [Fact]
    public void ProcessTemplate_SameLineLoop_ProcessesCorrectly()
    {
        // Arrange: Test loop where both {{#foreach}} and {{/foreach}} are in the same paragraph
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Before loop");
        builder.AddParagraph("{{#foreach Items}}{{.}}, {{/foreach}}");
        builder.AddParagraph("After loop");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Alpha", "Beta", "Gamma" }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(5, paragraphs.Count); // "Before loop" + 3 items + "After loop"
        Assert.Equal("Before loop", paragraphs[0]);
        Assert.Equal("Alpha, ", paragraphs[1]);
        Assert.Equal("Beta, ", paragraphs[2]);
        Assert.Equal("Gamma, ", paragraphs[3]);
        Assert.Equal("After loop", paragraphs[4]);
    }

    [Fact]
    public void ProcessTemplate_SameLineLoopWithComplexContent_ProcessesCorrectly()
    {
        // Arrange: Same-line loop with object properties
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Organisations: {{#foreach Organisations}}{{Name}} ({{Role}}){{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Organisations"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Name"] = "Acme Corp", ["Role"] = "Controller" },
                new Dictionary<string, object> { ["Name"] = "TechCorp", ["Role"] = "Processor" }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);

        DocumentVerifier verifier = run.Verifier;
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Should have 3 paragraphs: "Organisations: " header + 2 items
        Assert.Equal(3, paragraphs.Count);
        Assert.Contains("Organisations:", paragraphs[0]);
        Assert.Contains("Acme Corp (Controller)", paragraphs[1]);
        Assert.Contains("TechCorp (Processor)", paragraphs[2]);
    }

    [Fact]
    public void ProcessTemplate_NestedSameLineLoops_ProcessesCorrectly()
    {
        // Arrange: Nested loops both in same line
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Groups}}Group {{Name}}: {{#foreach Members}}{{.}}, {{/foreach}}{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Groups"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "A",
                    ["Members"] = new List<string> { "Alice", "Bob" }
                },
                new Dictionary<string, object>
                {
                    ["Name"] = "B",
                    ["Members"] = new List<string> { "Charlie", "David" }
                }
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        // Should have nested structure with both groups and their members
        Assert.Contains(paragraphs, p => p.Contains("Group A:"));
        Assert.Contains(paragraphs, p => p.Contains("Alice"));
        Assert.Contains(paragraphs, p => p.Contains("Bob"));
        Assert.Contains(paragraphs, p => p.Contains("Group B:"));
        Assert.Contains(paragraphs, p => p.Contains("Charlie"));
        Assert.Contains(paragraphs, p => p.Contains("David"));
    }
    */

    #region Null Handling (#147)

    private sealed class NoteItem
    {
        public string Name { get; set; } = "";
        public string? Notes { get; set; }
    }

    private sealed class NullableCategory
    {
        public string Name { get; set; } = "";
        public List<string?>? Products { get; set; }
    }

    private static (ProcessingResult Result, List<string> Paragraphs) ProcessParagraphs(
        Dictionary<string, object> data,
        params string[] paragraphs)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        MemoryStream templateStream = builder.ToStream();
        MemoryStream outputStream = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(templateStream, outputStream, data);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        return (result, verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void ProcessTemplate_JsonArrayWithNullItem_DoesNotFailAndRendersNullAsEmpty()
    {
        // Arrange
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary("{\"Tags\": [\"a\", null, \"b\"]}");

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data, "{{#foreach Tags}}", "[{{.}}]", "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "[a]", "[]", "[b]" }, paragraphs);
        Assert.Empty(result.MissingVariables);
        Assert.DoesNotContain(result.Warnings, w => w.Type == ProcessingWarningType.MissingVariable);
    }

    [Fact]
    public void ProcessTemplate_NullItem_ThisRendersEmpty()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Tags"] = new List<string?> { "a", null }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data, "{{#foreach Tags}}", "[{{this}}]", "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "[a]", "[]" }, paragraphs);
    }

    [Fact]
    public void ProcessTemplate_NullItem_LoopMetadataStillCorrect()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Tags"] = new List<string?> { "a", null, "b" }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data, "{{#foreach Tags}}", "{{@index}}:{{.}}:{{@count}}:{{@first}}:{{@last}}", "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "0:a:3:True:False", "1::3:False:False", "2:b:3:False:True" }, paragraphs);
    }

    [Fact]
    public void ProcessTemplate_NullItem_IfDotEvaluatesToFalse()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Tags"] = new List<string?> { "a", null }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data,
            "{{#foreach Tags}}",
            "{{#if .}}",
            "value {{.}}",
            "{{#else}}",
            "no value",
            "{{/if}}",
            "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "value a", "no value" }, paragraphs);
    }

    [Fact]
    public void ProcessTemplate_NullItem_ImplicitNameFallsThroughToGlobal()
    {
        // Arrange: a null item has no properties, so implicit names still reach the parent scope
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Company"] = "ACME",
            ["Tags"] = new List<string?> { "a", null }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data, "{{#foreach Tags}}", "{{Company}}:{{.}}", "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "ACME:a", "ACME:" }, paragraphs);
    }

    [Fact]
    public void ProcessTemplate_NullItemProperty_DoesNotLeakGlobalValue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Notes"] = "GLOBAL",
            ["Items"] = new List<NoteItem> { new NoteItem { Name = "A", Notes = null } }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data, "{{#foreach Items}}", "{{Name}}:{{Notes}}", "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "A:" }, paragraphs);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ProcessTemplate_NullDictionaryItemValue_DoesNotLeakGlobalValue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Notes"] = "GLOBAL",
            ["Items"] = new List<Dictionary<string, object?>>
            {
                new Dictionary<string, object?> { ["Name"] = "A", ["Notes"] = null },
                new Dictionary<string, object?> { ["Name"] = "B" }
            }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data, "{{#foreach Items}}", "{{Name}}:{{Notes}}", "{{/foreach}}");

        // Assert: present-but-null renders empty; a missing key still falls through to the global value
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "A:", "B:GLOBAL" }, paragraphs);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ProcessTemplate_NullJsonItemValue_DoesNotLeakGlobalValue()
    {
        // Arrange
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(
            "{\"Notes\": \"GLOBAL\", \"Items\": [{\"Name\": \"A\", \"Notes\": null}]}");

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data, "{{#foreach Items}}", "{{Name}}:{{Notes}}", "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "A:" }, paragraphs);
    }

    [Fact]
    public void ProcessTemplate_NullItemProperty_ConditionDoesNotUseGlobalValue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Notes"] = "GLOBAL",
            ["Items"] = new List<NoteItem> { new NoteItem { Name = "A", Notes = null } }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data,
            "{{#foreach Items}}",
            "{{#if Notes}}",
            "has notes",
            "{{#else}}",
            "no notes",
            "{{/if}}",
            "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "no notes" }, paragraphs);
    }

    [Fact]
    public void ProcessTemplate_NullItemProperty_ExistsIsTrue()
    {
        // Arrange: POCO and dictionary items, with present-null and missing properties
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Pocos"] = new List<NoteItem> { new NoteItem { Name = "P", Notes = null } },
            ["Dicts"] = new List<Dictionary<string, object?>>
            {
                new Dictionary<string, object?> { ["Name"] = "D1", ["Notes"] = null },
                new Dictionary<string, object?> { ["Name"] = "D2" }
            }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data,
            "{{#foreach Pocos}}",
            "{{#if Notes exists}}",
            "{{Name}} present",
            "{{#else}}",
            "{{Name}} absent",
            "{{/if}}",
            "{{/foreach}}",
            "{{#foreach Dicts}}",
            "{{#if Notes exists}}",
            "{{Name}} present",
            "{{#else}}",
            "{{Name}} absent",
            "{{/if}}",
            "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "P present", "D1 present", "D2 absent" }, paragraphs);
    }

    [Fact]
    public void ProcessTemplate_NestedLoop_NullInnerCollection_SkipsInnerLoop()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<NullableCategory>
            {
                new NullableCategory { Name = "A", Products = null },
                new NullableCategory { Name = "B", Products = new List<string?> { "x", null } }
            }
        };

        // Act
        (ProcessingResult result, List<string> paragraphs) = ProcessParagraphs(
            data,
            "{{#foreach Categories}}",
            "Category {{Name}}",
            "{{#foreach Products}}",
            "- [{{.}}]",
            "{{/foreach}}",
            "{{/foreach}}");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "Category A", "Category B", "- [x]", "- []" }, paragraphs);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.NullLoopCollection);
    }

    #endregion
}
