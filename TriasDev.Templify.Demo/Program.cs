// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using System.Globalization;

namespace TriasDev.Templify.Demo;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Templify Comprehensive Demo ===");
        Console.WriteLine();

        string outputDir = "/Users/vaceslavustinov/Documents";
        string templatePath = Path.Combine(outputDir, "Templify-Template.docx");
        string outputPath = Path.Combine(outputDir, "Templify-Output.docx");

        Console.WriteLine("🔨 Creating comprehensive template with all use cases...");
        CreateComprehensiveTemplate(templatePath);
        Console.WriteLine($"✅ Template created: {templatePath}");
        Console.WriteLine();

        Console.WriteLine("📊 Creating test data...");
        Dictionary<string, object> data = CreateComprehensiveTestData();
        Console.WriteLine("✅ Test data created");
        Console.WriteLine();

        Console.WriteLine("⚙️  Processing template...");
        ProcessingResult result = ProcessTemplate(templatePath, outputPath, data);

        Console.WriteLine();
        if (result.IsSuccess)
        {
            Console.WriteLine("✅ Template processed successfully!");
            Console.WriteLine($"   Replacements made: {result.ReplacementCount}");

            if (result.MissingVariables.Any())
            {
                Console.WriteLine($"   ⚠️  Missing variables: {string.Join(", ", result.MissingVariables)}");
            }

            Console.WriteLine();
            Console.WriteLine($"📁 Template: {templatePath}");
            Console.WriteLine($"📁 Output:   {outputPath}");
            Console.WriteLine();
            Console.WriteLine("💡 Open both files in Word to compare template vs output!");
        }
        else
        {
            Console.WriteLine($"❌ Processing failed: {result.ErrorMessage}");
        }

        // JSON Demo
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        DemonstrateJsonInput(outputDir);

        // Validation Demo
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        DemonstrateValidation(outputDir);

        // Real Process Template Demo
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        DemonstrateRealProcessTemplate();
    }

    static void CreateComprehensiveTemplate(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        // Title
        AddTitle(body, "Templify - Comprehensive Feature Demo");
        AddParagraph(body, "");

        // 1. Simple Placeholders
        AddHeading(body, "1. Simple Placeholder Replacement");
        AddParagraph(body, "Company: {{CompanyName}}");
        AddParagraph(body, "Date: {{Date}}");
        AddParagraph(body, "Document Number: {{DocumentNumber}}");
        AddParagraph(body, "Price: {{Price}} {{Currency}}");
        AddParagraph(body, "");

        // 2. Nested Properties
        AddHeading(body, "2. Nested Property Access (Dot Notation)");
        AddParagraph(body, "Customer Name: {{Customer.Name}}");
        AddParagraph(body, "Contact: {{Customer.ContactPerson}}");
        AddParagraph(body, "Email: {{Customer.Email}}");
        AddParagraph(body, "Address: {{Customer.Address.Street}}, {{Customer.Address.PostalCode}} {{Customer.Address.City}}");
        AddParagraph(body, "Country: {{Customer.Address.Country}}");
        AddParagraph(body, "");

        // 3. Array Indexing
        AddHeading(body, "3. Array/List Indexing");
        AddParagraph(body, "First Item: {{Items[0]}}");
        AddParagraph(body, "Second Item: {{Items[1]}}");
        AddParagraph(body, "Third Item: {{Items[2]}}");
        AddParagraph(body, "");

        // 4. Dictionary Access
        AddHeading(body, "4. Dictionary Access");
        AddParagraph(body, "Theme: {{Settings[Theme]}} or {{Settings.Theme}}");
        AddParagraph(body, "Language: {{Settings[Language]}} or {{Settings.Language}}");
        AddParagraph(body, "");

        // 5. Simple Loop
        AddHeading(body, "5. Simple Loop (Primitive Values)");
        AddParagraph(body, "{{#foreach Items}}");
        AddParagraph(body, "  • {{.}}");
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        // 6. Loop with Objects
        AddHeading(body, "6. Loop with Objects");
        AddParagraph(body, "{{#foreach LineItems}}");
        AddParagraph(body, "  {{Position}}. {{Product}} - Qty: {{Quantity}} @ {{UnitPrice}} EUR = {{Total}} EUR");
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        // 7. Loop Metadata
        AddHeading(body, "7. Loop Metadata (index, first, last, count)");
        AddParagraph(body, "{{#foreach Tags}}");
        AddParagraph(body, "  [{{@index}}] {{.}} (First: {{@first}}, Last: {{@last}}, Total: {{@count}})");
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        // 8. Nested Loops
        AddHeading(body, "8. Nested Loops");
        AddParagraph(body, "{{#foreach Orders}}");
        AddParagraph(body, "Order #{{OrderId}} - Total: {{Total}} EUR");
        AddParagraph(body, "  Items:");
        AddParagraph(body, "  {{#foreach Items}}");
        AddParagraph(body, "    - {{Product}} (Qty: {{Quantity}})");
        AddParagraph(body, "  {{/foreach}}");
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        // 9. Table with Placeholders
        AddHeading(body, "9. Table with Placeholders");
        Table table = CreateTable(body, 4, 3);
        SetCellText(table, 0, 0, "Position");
        SetCellText(table, 0, 1, "Product");
        SetCellText(table, 0, 2, "Price");
        SetCellText(table, 1, 0, "{{LineItems[0].Position}}");
        SetCellText(table, 1, 1, "{{LineItems[0].Product}}");
        SetCellText(table, 1, 2, "{{LineItems[0].UnitPrice}}");
        SetCellText(table, 2, 0, "{{LineItems[1].Position}}");
        SetCellText(table, 2, 1, "{{LineItems[1].Product}}");
        SetCellText(table, 2, 2, "{{LineItems[1].UnitPrice}}");
        SetCellText(table, 3, 0, "{{LineItems[2].Position}}");
        SetCellText(table, 3, 1, "{{LineItems[2].Product}}");
        SetCellText(table, 3, 2, "{{LineItems[2].UnitPrice}}");
        AddParagraph(body, "");

        // 10. Table Row Loop
        AddHeading(body, "10. Table Row Loop");
        Table loopTable = CreateTable(body, 4, 3);
        SetCellText(loopTable, 0, 0, "Position");
        SetCellText(loopTable, 0, 1, "Product");
        SetCellText(loopTable, 0, 2, "Total");
        SetCellText(loopTable, 1, 0, "{{#foreach LineItems}}");
        SetCellText(loopTable, 2, 0, "{{Position}}");
        SetCellText(loopTable, 2, 1, "{{Product}}");
        SetCellText(loopTable, 2, 2, "{{Total}}");
        SetCellText(loopTable, 3, 0, "{{/foreach}}");
        AddParagraph(body, "");

        // 11. Formatting Preservation
        AddHeading(body, "11. Formatting Preservation");
        AddFormattedParagraph(body, "Bold text with placeholder: {{CompanyName}}", bold: true);
        AddFormattedParagraph(body, "Italic text with placeholder: {{Date}}", italic: true);
        AddParagraph(body, "");

        // 12. Number/Boolean/Date Formatting
        AddHeading(body, "12. Different Data Types");
        AddParagraph(body, "Integer: {{Quantity}}");
        AddParagraph(body, "Decimal: {{Price}}");
        AddParagraph(body, "Boolean (true): {{IsApproved}}");
        AddParagraph(body, "Boolean (false): {{IsPaid}}");
        AddParagraph(body, "Date: {{Date}}");
        AddParagraph(body, "");

        // 13. Culture-Specific Formatting
        AddHeading(body, "13. Culture-Specific Formatting");
        AddParagraph(body, "German format (comma decimal): {{GermanPrice}}");
        AddParagraph(body, "US format (dot decimal): {{USPrice}}");
        AddParagraph(body, "");

        // 14. Simple Conditionals
        AddHeading(body, "14. Simple Conditionals (If/Else)");
        AddParagraph(body, "{{#if IsApproved}}");
        AddParagraph(body, "  ✅ Status: APPROVED");
        AddParagraph(body, "{{else}}");
        AddParagraph(body, "  ⏳ Status: PENDING");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if IsPaid}}");
        AddParagraph(body, "  💰 Payment: RECEIVED");
        AddParagraph(body, "{{else}}");
        AddParagraph(body, "  ⚠️  Payment: OUTSTANDING");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 15. Conditionals with Comparison Operators
        AddHeading(body, "15. Conditionals with Comparison Operators");
        AddParagraph(body, "{{#if Price gt 1000}}");
        AddParagraph(body, "  🎉 HIGH VALUE ITEM (Price > 1000)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if Quantity gte 10}}");
        AddParagraph(body, "  📦 BULK ORDER DISCOUNT APPLIED");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if Customer.Address.Country eq Germany}}");
        AddParagraph(body, "  🇩🇪 Domestic Shipping: 2-3 business days");
        AddParagraph(body, "{{else}}");
        AddParagraph(body, "  ✈️ International Shipping: 5-7 business days");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 16. Conditionals with Logical Operators
        AddHeading(body, "16. Conditionals with Logical Operators (and/or/not)");
        AddParagraph(body, "{{#if IsApproved and Quantity gt 5}}");
        AddParagraph(body, "  ✅ Approved bulk order - Priority processing");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if Price gt 500 and Price lt 2000}}");
        AddParagraph(body, "  💰 Mid-range product (€500 - €2000)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if not IsPaid}}");
        AddParagraph(body, "  ⚠️  REMINDER: Payment pending");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 17. Nested Conditionals
        AddHeading(body, "17. Nested Conditionals");
        AddParagraph(body, "{{#if IsVIPCustomer}}");
        AddParagraph(body, "  ⭐ VIP CUSTOMER");
        AddParagraph(body, "  {{#if HasActiveSubscription}}");
        AddParagraph(body, "    ✅ Active Subscription");
        AddParagraph(body, "    {{#if SubscriptionTier eq Premium}}");
        AddParagraph(body, "      🏆 PREMIUM TIER - All benefits included");
        AddParagraph(body, "    {{else}}");
        AddParagraph(body, "      💼 STANDARD TIER");
        AddParagraph(body, "    {{/if}}");
        AddParagraph(body, "  {{else}}");
        AddParagraph(body, "    ⏳ Subscription expired - Contact sales");
        AddParagraph(body, "  {{/if}}");
        AddParagraph(body, "{{else}}");
        AddParagraph(body, "  📋 STANDARD CUSTOMER");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 18. Conditionals with Loops
        AddHeading(body, "18. Conditionals Combined with Loops");
        AddParagraph(body, "Order Items (with conditional pricing):");
        AddParagraph(body, "{{#foreach LineItems}}");
        AddParagraph(body, "  {{Position}}. {{Product}} - {{Total}} EUR");
        AddParagraph(body, "  {{#if Total gt 1000}}");
        AddParagraph(body, "    💎 Premium item - Extended warranty included");
        AddParagraph(body, "  {{/if}}");
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        // 19. Lists in Loops
        AddHeading(body, "19. Lists in Loops");
        AddParagraph(body, "Product Features (Bullet List):");
        AddParagraph(body, "{{#foreach Features}}");
        AddBulletListItem(body, "{{.}}", doc);
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        AddParagraph(body, "Setup Steps (Numbered List):");
        AddParagraph(body, "{{#foreach SetupSteps}}");
        AddNumberedListItem(body, "{{.}}", doc);
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        AddParagraph(body, "Available Products (Bullet List with Objects):");
        AddParagraph(body, "{{#foreach AvailableProducts}}");
        AddBulletListItem(body, "{{Name}} - {{Price}} EUR", doc);
        AddParagraph(body, "{{/foreach}}");
        AddParagraph(body, "");

        // Footer
        AddParagraph(body, "");
        AddParagraph(body, "────────────────────────────────────────");
        AddParagraph(body, "End of Template Demo");

        mainPart.Document.Save();
    }

    static Dictionary<string, object> CreateComprehensiveTestData()
    {
        return new Dictionary<string, object>
        {
            // Simple values
            ["CompanyName"] = "TriasDev GmbH & Co. KG",
            ["Date"] = DateTime.Now,
            ["DocumentNumber"] = "DOC-2025-001",
            ["Price"] = 1234.56m,
            ["Currency"] = "EUR",
            ["Quantity"] = 42,
            ["IsApproved"] = true,
            ["IsPaid"] = false,
            ["GermanPrice"] = 1234.56m,
            ["USPrice"] = 1234.56m,

            // Conditional test data
            ["IsVIPCustomer"] = true,
            ["HasActiveSubscription"] = true,
            ["SubscriptionTier"] = "Premium",

            // Nested object
            ["Customer"] = new Customer
            {
                Name = "Acme Corporation",
                ContactPerson = "Max Mustermann",
                Email = "max.mustermann@acme.com",
                Phone = "+49 89 123456",
                Address = new Address
                {
                    Street = "Hauptstraße 123",
                    PostalCode = "80331",
                    City = "Munich",
                    Country = "Germany"
                }
            },

            // Simple list
            ["Items"] = new List<string>
            {
                "Item One",
                "Item Two",
                "Item Three"
            },

            // Tags for metadata demo
            ["Tags"] = new List<string>
            {
                "urgent",
                "approved",
                "completed"
            },

            // Features for bullet list demo
            ["Features"] = new List<string>
            {
                "Advanced GDPR compliance tracking",
                "Automated risk assessments",
                "Real-time reporting and analytics",
                "Multi-language support"
            },

            // Steps for numbered list demo
            ["SetupSteps"] = new List<string>
            {
                "Download and install the application",
                "Configure your organization settings",
                "Import existing data",
                "Train your team",
                "Start using the system"
            },

            // Products for list with objects demo
            ["AvailableProducts"] = new List<ProductInfo>
            {
                new ProductInfo { Name = "Enterprise Edition", Price = 999.00m, Available = true },
                new ProductInfo { Name = "Professional Edition", Price = 499.00m, Available = true },
                new ProductInfo { Name = "Starter Edition", Price = 199.00m, Available = true }
            },

            // Complex list
            ["LineItems"] = new List<LineItem>
            {
                new LineItem
                {
                    Position = 1,
                    Product = "Software Enterprise License",
                    Quantity = 5,
                    UnitPrice = 499.00m,
                    Total = 2495.00m
                },
                new LineItem
                {
                    Position = 2,
                    Product = "Annual Support & Maintenance",
                    Quantity = 5,
                    UnitPrice = 99.00m,
                    Total = 495.00m
                },
                new LineItem
                {
                    Position = 3,
                    Product = "Training Package (2 days)",
                    Quantity = 2,
                    UnitPrice = 250.00m,
                    Total = 500.00m
                }
            },

            // Nested loops data
            ["Orders"] = new List<Order>
            {
                new Order
                {
                    OrderId = "ORD-001",
                    Total = 1250.00m,
                    Items = new List<OrderItem>
                    {
                        new OrderItem { Product = "Laptop", Quantity = 2 },
                        new OrderItem { Product = "Mouse", Quantity = 5 }
                    }
                },
                new Order
                {
                    OrderId = "ORD-002",
                    Total = 750.50m,
                    Items = new List<OrderItem>
                    {
                        new OrderItem { Product = "Keyboard", Quantity = 3 },
                        new OrderItem { Product = "Monitor", Quantity = 1 }
                    }
                }
            },

            // Dictionary
            ["Settings"] = new Dictionary<string, string>
            {
                ["Theme"] = "Professional",
                ["Language"] = "German",
                ["TimeZone"] = "CET"
            }
        };
    }

    static ProcessingResult ProcessTemplate(string templatePath, string outputPath, Dictionary<string, object> data)
    {
        try
        {
            PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
            {
                MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                Culture = CultureInfo.InvariantCulture
            };

            DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

            using FileStream templateStream = File.OpenRead(templatePath);
            using FileStream outputStream = File.Create(outputPath);

            return processor.ProcessTemplate(templateStream, outputStream, data);
        }
        catch (Exception ex)
        {
            return ProcessingResult.Failure($"Exception: {ex.Message}");
        }
    }

    static void DemonstrateJsonInput(string outputDir)
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

    static void CreateSimpleJsonTemplate(string filePath)
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
        AddParagraph(body, "{{else}}");
        AddParagraph(body, "  ⏳ Status: PENDING");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if IsPaid}}");
        AddParagraph(body, "  💰 Payment: RECEIVED");
        AddParagraph(body, "{{else}}");
        AddParagraph(body, "  ⚠️  Payment: OUTSTANDING");
        AddParagraph(body, "{{/if}}");

        mainPart.Document.Save();
    }

    static void DemonstrateValidation(string outputDir)
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

    static void DemonstrateRealProcessTemplate()
    {
        Console.WriteLine("=== Real Process Template Demo ===");
        Console.WriteLine();
        Console.WriteLine("Testing the converted template with real process data!");
        Console.WriteLine();

        // Find the project directory by looking for the Data folder
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string? projectDir = FindProjectDirectory(baseDir);

        if (projectDir == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Could not locate project directory with Data folder");
            Console.ResetColor();
            return;
        }

        string dataDir = Path.Combine(projectDir, "Data");
        string templatePath = Path.Combine(dataDir, "Template Verarbeitungstätigkeit-Full_en-templify.docx");
        string jsonPath = Path.Combine(dataDir, "process-data-hash.json");
        string outputPath = "/Users/vaceslavustinov/Documents/Process-Output.docx";

        // Verify files exist
        if (!File.Exists(templatePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Template not found: {templatePath}");
            Console.ResetColor();
            return;
        }

        if (!File.Exists(jsonPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ JSON data not found: {jsonPath}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"📄 Template: {Path.GetFileName(templatePath)}");
        Console.WriteLine($"📊 Data: {Path.GetFileName(jsonPath)}");
        Console.WriteLine();

        try
        {
            // Load JSON data
            Console.WriteLine("📥 Loading JSON data...");
            string jsonData = File.ReadAllText(jsonPath);
            FileInfo jsonFileInfo = new FileInfo(jsonPath);
            Console.WriteLine($"   ✅ Loaded {jsonFileInfo.Length / 1024 / 1024:F2} MB of JSON data");
            Console.WriteLine();

            // Setup processor
            PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
            {
                MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                Culture = CultureInfo.InvariantCulture
            };

            DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

            // First, validate the template
            Console.WriteLine("🔍 Validating template...");
            using (FileStream validateStream = File.OpenRead(templatePath))
            {
                ValidationResult validation = processor.ValidateTemplate(validateStream);

                if (validation.IsValid)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"   ✅ Template is valid!");
                    Console.ResetColor();
                    Console.WriteLine($"   📝 Found {validation.AllPlaceholders.Count} unique placeholders");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   ⚠️  Template has {validation.Errors.Count} validation errors:");
                    Console.ResetColor();
                    foreach (ValidationError error in validation.Errors)
                    {
                        Console.WriteLine($"      - {error.Type}: {error.Message}");
                    }
                }
            }

            Console.WriteLine();

            // Process the template
            Console.WriteLine("⚙️  Processing template with JSON data...");
            Console.WriteLine("   This may take a moment for large templates...");
            Console.WriteLine();

            using (FileStream templateStream = File.OpenRead(templatePath))
            using (FileStream outputStream = File.Create(outputPath))
            {
                ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

                Console.WriteLine();
                if (result.IsSuccess)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✅ Template processed successfully!");
                    Console.ResetColor();
                    Console.WriteLine();

                    Console.WriteLine("📊 PROCESSING STATISTICS:");
                    Console.WriteLine($"   • Replacements made: {result.ReplacementCount}");

                    if (result.MissingVariables.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"   • Missing variables: {result.MissingVariables.Count}");
                        Console.ResetColor();

                        if (result.MissingVariables.Count <= 10)
                        {
                            foreach (string missing in result.MissingVariables)
                            {
                                Console.WriteLine($"      - {missing}");
                            }
                        }
                        else
                        {
                            foreach (string missing in result.MissingVariables.Take(10))
                            {
                                Console.WriteLine($"      - {missing}");
                            }
                            Console.WriteLine($"      ... and {result.MissingVariables.Count - 10} more");
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("   • Missing variables: 0 (All placeholders filled!)");
                        Console.ResetColor();
                    }

                    Console.WriteLine();
                    Console.WriteLine("📁 OUTPUT:");
                    Console.WriteLine($"   Template: {templatePath}");
                    Console.WriteLine($"   Output:   {outputPath}");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("💡 Open the output file in Word to see the rendered template!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ Processing failed: {result.ErrorMessage}");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Exception occurred: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
            Console.ResetColor();
        }
    }

    static string? FindProjectDirectory(string startDir)
    {
        DirectoryInfo? current = new DirectoryInfo(startDir);

        while (current != null)
        {
            string dataPath = Path.Combine(current.FullName, "Data");
            if (Directory.Exists(dataPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    static void CreateValidTemplate(string filePath)
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

    static void CreateTemplateWithUnmatchedConditional(string filePath)
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

    static void CreateTemplateWithUnmatchedLoop(string filePath)
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

    static void CreateTemplateForDataValidation(string filePath)
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

    // Helper methods for document creation
    static void AddTitle(Body body, string text)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        RunProperties props = run.AppendChild(new RunProperties());
        props.AppendChild(new Bold());
        props.AppendChild(new FontSize { Val = "32" });
        run.AppendChild(new Text(text));
    }

    static void AddHeading(Body body, string text)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        RunProperties props = run.AppendChild(new RunProperties());
        props.AppendChild(new Bold());
        props.AppendChild(new FontSize { Val = "24" });
        run.AppendChild(new Text(text));
    }

    static void AddParagraph(Body body, string text)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    static void AddFormattedParagraph(Body body, string text, bool bold = false, bool italic = false)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        RunProperties props = run.AppendChild(new RunProperties());

        if (bold) props.AppendChild(new Bold());
        if (italic) props.AppendChild(new Italic());

        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    static void AddBulletListItem(Body body, string text, WordprocessingDocument document)
    {
        EnsureNumberingPart(document);

        Paragraph para = body.AppendChild(new Paragraph());

        // Apply bullet numbering
        ParagraphProperties paraProps = para.AppendChild(new ParagraphProperties());
        NumberingProperties numProps = new NumberingProperties(
            new NumberingLevelReference() { Val = 0 },
            new NumberingId() { Val = 1 } // Bullet list ID
        );
        paraProps.AppendChild(numProps);

        Run run = para.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    static void AddNumberedListItem(Body body, string text, WordprocessingDocument document)
    {
        EnsureNumberingPart(document);

        Paragraph para = body.AppendChild(new Paragraph());

        // Apply numbered list numbering
        ParagraphProperties paraProps = para.AppendChild(new ParagraphProperties());
        NumberingProperties numProps = new NumberingProperties(
            new NumberingLevelReference() { Val = 0 },
            new NumberingId() { Val = 2 } // Numbered list ID
        );
        paraProps.AppendChild(numProps);

        Run run = para.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    static void EnsureNumberingPart(WordprocessingDocument document)
    {
        MainDocumentPart mainPart = document.MainDocumentPart!;

        if (mainPart.NumberingDefinitionsPart == null)
        {
            NumberingDefinitionsPart numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
            Numbering numbering = new Numbering();

            // Create bullet list definition (AbstractNum ID 0, Num ID 1)
            AbstractNum bulletAbstractNum = new AbstractNum() { AbstractNumberId = 0 };
            Level bulletLevel = new Level() { LevelIndex = 0 };
            bulletLevel.AppendChild(new NumberingFormat() { Val = NumberFormatValues.Bullet });
            bulletLevel.AppendChild(new LevelText() { Val = "·" });
            bulletLevel.AppendChild(new LevelJustification() { Val = LevelJustificationValues.Left });
            PreviousParagraphProperties bulletPPr = new PreviousParagraphProperties();
            bulletPPr.AppendChild(new Indentation() { Left = "720", Hanging = "360" });
            bulletLevel.AppendChild(bulletPPr);
            bulletAbstractNum.AppendChild(bulletLevel);
            numbering.AppendChild(bulletAbstractNum);

            NumberingInstance bulletNum = new NumberingInstance() { NumberID = 1 };
            bulletNum.AppendChild(new AbstractNumId() { Val = 0 });
            numbering.AppendChild(bulletNum);

            // Create numbered list definition (AbstractNum ID 1, Num ID 2)
            AbstractNum numberedAbstractNum = new AbstractNum() { AbstractNumberId = 1 };
            Level numberedLevel = new Level() { LevelIndex = 0 };
            numberedLevel.AppendChild(new StartNumberingValue() { Val = 1 });
            numberedLevel.AppendChild(new NumberingFormat() { Val = NumberFormatValues.Decimal });
            numberedLevel.AppendChild(new LevelText() { Val = "%1." });
            numberedLevel.AppendChild(new LevelJustification() { Val = LevelJustificationValues.Left });
            PreviousParagraphProperties numberedPPr = new PreviousParagraphProperties();
            numberedPPr.AppendChild(new Indentation() { Left = "720", Hanging = "360" });
            numberedLevel.AppendChild(numberedPPr);
            numberedAbstractNum.AppendChild(numberedLevel);
            numbering.AppendChild(numberedAbstractNum);

            NumberingInstance numberedNum = new NumberingInstance() { NumberID = 2 };
            numberedNum.AppendChild(new AbstractNumId() { Val = 1 });
            numbering.AppendChild(numberedNum);

            numberingPart.Numbering = numbering;
            numberingPart.Numbering.Save();
        }
    }

    static Table CreateTable(Body body, int rows, int cols)
    {
        Table table = new Table();

        TableProperties props = new TableProperties(
            new TableBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
            )
        );

        table.AppendChild(props);

        for (int r = 0; r < rows; r++)
        {
            TableRow row = new TableRow();
            for (int c = 0; c < cols; c++)
            {
                TableCell cell = new TableCell();
                cell.Append(new Paragraph(new Run(new Text(""))));
                row.Append(cell);
            }
            table.Append(row);
        }

        body.Append(table);
        return table;
    }

    static void SetCellText(Table table, int row, int col, string text)
    {
        TableRow? tr = table.Elements<TableRow>().ElementAtOrDefault(row);
        TableCell? cell = tr?.Elements<TableCell>().ElementAtOrDefault(col);
        if (cell != null)
        {
            Paragraph? para = cell.Elements<Paragraph>().FirstOrDefault();
            if (para != null)
            {
                para.RemoveAllChildren<Run>();
                para.AppendChild(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            }
        }
    }
}

// Data classes
public class Customer
{
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Address? Address { get; set; }
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class LineItem
{
    public int Position { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ProductInfo
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool Available { get; set; }
}
