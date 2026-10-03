// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Demo;

/// <summary>
/// JSON input demo: processes a template with a JSON string as data.
/// </summary>
internal partial class Program
{
    private static void DemonstrateJsonInput(string outputDir)
    {
        Console.WriteLine("=== JSON Input Demo ===");
        Console.WriteLine();
        Console.WriteLine("The Templify library now supports JSON strings as input!");
        Console.WriteLine("This is useful when you receive data from APIs, databases, or configuration files.");
        Console.WriteLine();

        // Create a simple template
        string jsonTemplatePath = Path.Combine(outputDir, "Templify-JSON-Template.docx");
        string jsonOutputPath = Path.Combine(outputDir, "Templify-JSON-Output.docx");

        Console.WriteLine("🔨 Creating simple template for JSON demo...");
        CreateSimpleJsonTemplate(jsonTemplatePath);
        Console.WriteLine($"✅ Template created: {jsonTemplatePath}");
        Console.WriteLine();

        // Create JSON data string
        string jsonData = """
            {
                "CompanyName": "TriasDev GmbH & Co. KG",
                "Date": "2025-11-10",
                "Customer": {
                    "Name": "Max Mustermann",
                    "Email": "max.mustermann@example.com",
                    "Address": {
                        "Street": "Hauptstraße 123",
                        "City": "Munich",
                        "PostalCode": "80331",
                        "Country": "Germany"
                    }
                },
                "LineItems": [
                    { "Position": 1, "Product": "Premium Widget", "Quantity": 2, "UnitPrice": 299.99, "Total": 599.98 },
                    { "Position": 2, "Product": "Deluxe Gadget", "Quantity": 1, "UnitPrice": 499.99, "Total": 499.99 },
                    { "Position": 3, "Product": "Standard Tool", "Quantity": 5, "UnitPrice": 49.99, "Total": 249.95 }
                ],
                "Total": 1349.92,
                "IsApproved": true,
                "IsPaid": false
            }
            """;

        Console.WriteLine("📋 JSON Input Data:");
        Console.WriteLine(jsonData);
        Console.WriteLine();

        Console.WriteLine("⚙️  Processing template with JSON input...");

        try
        {
            PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
            {
                MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                Culture = CultureInfo.InvariantCulture
            };

            DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

            using FileStream templateStream = File.OpenRead(jsonTemplatePath);
            using FileStream outputStream = File.Create(jsonOutputPath);

            // Process template using JSON string directly
            ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

            Console.WriteLine();
            if (result.IsSuccess)
            {
                Console.WriteLine("✅ Template processed successfully with JSON input!");
                Console.WriteLine($"   Replacements made: {result.ReplacementCount}");

                if (result.MissingVariables.Any())
                {
                    Console.WriteLine($"   ⚠️  Missing variables: {string.Join(", ", result.MissingVariables)}");
                }

                Console.WriteLine();
                Console.WriteLine($"📁 Template: {jsonTemplatePath}");
                Console.WriteLine($"📁 Output:   {jsonOutputPath}");
                Console.WriteLine();
                Console.WriteLine("💡 The JSON input produced the same result as using a Dictionary!");
                Console.WriteLine("💡 Both approaches are supported - choose what works best for your use case.");
            }
            else
            {
                Console.WriteLine($"❌ Processing failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
        }
    }

    private static void CreateSimpleJsonTemplate(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        // Title
        AddTitle(body, "Invoice - JSON Demo");
        AddParagraph(body, "");

        // Company and Date
        AddParagraph(body, "Company: {{CompanyName}}");
        AddParagraph(body, "Date: {{Date}}");
        AddParagraph(body, "");

        // Customer Information (nested object)
        AddHeading(body, "Customer Information");
        AddParagraph(body, "Name: {{Customer.Name}}");
        AddParagraph(body, "Email: {{Customer.Email}}");
        AddParagraph(body, "Address: {{Customer.Address.Street}}, {{Customer.Address.PostalCode}} {{Customer.Address.City}}");
        AddParagraph(body, "Country: {{Customer.Address.Country}}");
        AddParagraph(body, "");

        // Line Items (array/loop)
        AddHeading(body, "Line Items");
        AddParagraph(body, "{{#foreach LineItems}}");
        AddParagraph(body, "  {{Position}}. {{Product}} - Qty: {{Quantity}} @ €{{UnitPrice}} = €{{Total}}");
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        // Total
        AddParagraph(body, "Total Amount: €{{Total}}");
        AddParagraph(body, "");

        // Status (conditionals)
        AddHeading(body, "Status");
        AddParagraph(body, "{{#if IsApproved}}");
        AddParagraph(body, "  ✅ Status: APPROVED");
        AddParagraph(body, "{{#else}}");
        AddParagraph(body, "  ⏳ Status: PENDING");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if IsPaid}}");
        AddParagraph(body, "  💰 Payment: RECEIVED");
        AddParagraph(body, "{{#else}}");
        AddParagraph(body, "  ⚠️  Payment: OUTSTANDING");
        AddParagraph(body, "{{/if}}");

        mainPart.Document.Save();
    }
}
