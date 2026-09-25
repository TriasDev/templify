// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Demo;

/// <summary>
/// Template validation demo: valid/invalid templates and missing-variable detection.
/// </summary>
internal partial class Program
{
    private static void DemonstrateValidation(string outputDir)
    {
        Console.WriteLine("=== Template Validation Demo ===");
        Console.WriteLine();
        Console.WriteLine("Templify can validate templates for errors before processing them!");
        Console.WriteLine("This helps catch issues early and provide better error messages.");
        Console.WriteLine();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Example 1: Valid Template
        Console.WriteLine("📋 Example 1: Valid Template");
        string validTemplatePath = Path.Combine(outputDir, "Templify-Valid-Template.docx");
        CreateValidTemplate(validTemplatePath);

        using (FileStream validStream = File.OpenRead(validTemplatePath))
        {
            ValidationResult result = processor.ValidateTemplate(validStream);

            if (result.IsValid)
            {
                Console.WriteLine("  ✅ Template is valid!");
                Console.WriteLine($"  📝 Found {result.AllPlaceholders.Count} placeholders: {string.Join(", ", result.AllPlaceholders)}");
            }
            else
            {
                Console.WriteLine("  ❌ Template has errors!");
                foreach (ValidationError error in result.Errors)
                {
                    Console.WriteLine($"     - {error.Type}: {error.Message}");
                }
            }
        }

        Console.WriteLine();

        // Example 2: Template with Unmatched Conditional
        Console.WriteLine("📋 Example 2: Template with Unmatched Conditional");
        string invalidConditionalPath = Path.Combine(outputDir, "Templify-Invalid-Conditional.docx");
        CreateTemplateWithUnmatchedConditional(invalidConditionalPath);

        using (FileStream invalidStream = File.OpenRead(invalidConditionalPath))
        {
            ValidationResult result = processor.ValidateTemplate(invalidStream);

            if (result.IsValid)
            {
                Console.WriteLine("  ✅ Template is valid!");
            }
            else
            {
                Console.WriteLine("  ❌ Template has validation errors:");
                foreach (ValidationError error in result.Errors)
                {
                    Console.WriteLine($"     - {error.Type}: {error.Message}");
                }
            }
        }

        Console.WriteLine();

        // Example 3: Template with Unmatched Loop
        Console.WriteLine("📋 Example 3: Template with Unmatched Loop");
        string invalidLoopPath = Path.Combine(outputDir, "Templify-Invalid-Loop.docx");
        CreateTemplateWithUnmatchedLoop(invalidLoopPath);

        using (FileStream invalidStream = File.OpenRead(invalidLoopPath))
        {
            ValidationResult result = processor.ValidateTemplate(invalidStream);

            if (result.IsValid)
            {
                Console.WriteLine("  ✅ Template is valid!");
            }
            else
            {
                Console.WriteLine("  ❌ Template has validation errors:");
                foreach (ValidationError error in result.Errors)
                {
                    Console.WriteLine($"     - {error.Type}: {error.Message}");
                }
            }
        }

        Console.WriteLine();

        // Example 4: Validation with Data (Check Missing Variables)
        Console.WriteLine("📋 Example 4: Validation with Data (Missing Variables Check)");
        string templateForDataPath = Path.Combine(outputDir, "Templify-Data-Validation.docx");
        CreateTemplateForDataValidation(templateForDataPath);

        Dictionary<string, object> incompleteData = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["Email"] = "john@example.com"
            // Missing: Age, Phone
        };

        using (FileStream templateStream = File.OpenRead(templateForDataPath))
        {
            ValidationResult result = processor.ValidateTemplate(templateStream, incompleteData);

            if (result.IsValid)
            {
                Console.WriteLine("  ✅ All required variables are provided!");
            }
            else
            {
                Console.WriteLine("  ❌ Validation found issues:");
                Console.WriteLine($"     Missing variables: {string.Join(", ", result.MissingVariables)}");
                foreach (ValidationError error in result.Errors)
                {
                    Console.WriteLine($"     - {error.Type}: {error.Message}");
                }
            }
        }

        Console.WriteLine();

        // Example 5: Validation with Complete Data
        Console.WriteLine("📋 Example 5: Validation with Complete Data");
        Dictionary<string, object> completeData = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["Email"] = "john@example.com",
            ["Age"] = 30,
            ["Phone"] = "+1-234-567-8900"
        };

        using (FileStream templateStream = File.OpenRead(templateForDataPath))
        {
            ValidationResult result = processor.ValidateTemplate(templateStream, completeData);

            if (result.IsValid)
            {
                Console.WriteLine("  ✅ Template is valid and all variables are provided!");
                Console.WriteLine($"  📝 Placeholders: {string.Join(", ", result.AllPlaceholders)}");
                Console.WriteLine("  💡 Ready to process without errors!");
            }
            else
            {
                Console.WriteLine("  ❌ Validation found issues:");
                foreach (ValidationError error in result.Errors)
                {
                    Console.WriteLine($"     - {error.Type}: {error.Message}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("💡 Key Benefits of Template Validation:");
        Console.WriteLine("   • Catch syntax errors before processing");
        Console.WriteLine("   • Verify all data is available");
        Console.WriteLine("   • Get detailed error messages");
        Console.WriteLine("   • Improve user experience");
    }

    private static void CreateValidTemplate(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        AddTitle(body, "Valid Template Example");
        AddParagraph(body, "");
        AddParagraph(body, "Customer: {{CustomerName}}");
        AddParagraph(body, "Order Date: {{OrderDate}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if IsApproved}}");
        AddParagraph(body, "Status: Approved");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "Items:");
        AddParagraph(body, "{{#foreach Items}}");
        AddParagraph(body, "- {{Name}}");
        AddParagraph(body, "{{/foreach}}");

        mainPart.Document.Save();
    }

    private static void CreateTemplateWithUnmatchedConditional(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        AddTitle(body, "Template with Unmatched Conditional");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if IsApproved}}");
        AddParagraph(body, "This conditional is never closed!");
        AddParagraph(body, "");
        AddParagraph(body, "More content here...");

        mainPart.Document.Save();
    }

    private static void CreateTemplateWithUnmatchedLoop(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        AddTitle(body, "Template with Unmatched Loop");
        AddParagraph(body, "");
        AddParagraph(body, "{{#foreach Items}}");
        AddParagraph(body, "- {{Name}}");
        AddParagraph(body, "");
        AddParagraph(body, "This loop is never closed!");

        mainPart.Document.Save();
    }

    private static void CreateTemplateForDataValidation(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        AddTitle(body, "Template for Data Validation");
        AddParagraph(body, "");
        AddParagraph(body, "Name: {{Name}}");
        AddParagraph(body, "Email: {{Email}}");
        AddParagraph(body, "Age: {{Age}}");
        AddParagraph(body, "Phone: {{Phone}}");

        mainPart.Document.Save();
    }
}
