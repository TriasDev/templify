// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Demo;

/// <summary>
/// Comprehensive demo: one template covering placeholders, formatting, conditionals and loops.
/// </summary>
internal partial class Program
{
    private static void CreateComprehensiveTemplate(string filePath)
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
        AddParagraph(body, "{{#else}}");
        AddParagraph(body, "  ⏳ Status: PENDING");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if IsPaid}}");
        AddParagraph(body, "  💰 Payment: RECEIVED");
        AddParagraph(body, "{{#else}}");
        AddParagraph(body, "  ⚠️  Payment: OUTSTANDING");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 15. Conditionals with Comparison Operators
        AddHeading(body, "15. Conditionals with Comparison Operators");
        AddParagraph(body, "{{#if Price > 1000}}");
        AddParagraph(body, "  🎉 HIGH VALUE ITEM (Price > 1000)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if Quantity >= 10}}");
        AddParagraph(body, "  📦 BULK ORDER DISCOUNT APPLIED");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if Customer.Address.Country = \"Germany\"}}");
        AddParagraph(body, "  🇩🇪 Domestic Shipping: 2-3 business days");
        AddParagraph(body, "{{#else}}");
        AddParagraph(body, "  ✈️ International Shipping: 5-7 business days");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 16. Conditionals with Logical Operators
        AddHeading(body, "16. Conditionals with Logical Operators (and/or/not)");
        AddParagraph(body, "{{#if IsApproved and Quantity > 5}}");
        AddParagraph(body, "  ✅ Approved bulk order - Priority processing");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");
        AddParagraph(body, "{{#if Price > 500 and Price < 2000}}");
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
        AddParagraph(body, "    {{#if SubscriptionTier = \"Premium\"}}");
        AddParagraph(body, "      🏆 PREMIUM TIER - All benefits included");
        AddParagraph(body, "    {{#else}}");
        AddParagraph(body, "      💼 STANDARD TIER");
        AddParagraph(body, "    {{/if}}");
        AddParagraph(body, "  {{#else}}");
        AddParagraph(body, "    ⏳ Subscription expired - Contact sales");
        AddParagraph(body, "  {{/if}}");
        AddParagraph(body, "{{#else}}");
        AddParagraph(body, "  📋 STANDARD CUSTOMER");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 18. Conditionals with Loops
        AddHeading(body, "18. Conditionals Combined with Loops");
        AddParagraph(body, "Order Items (with conditional pricing):");
        AddParagraph(body, "{{#foreach LineItems}}");
        AddParagraph(body, "  {{Position}}. {{Product}} - {{Total}} EUR");
        AddParagraph(body, "  {{#if Total > 1000}}");
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

        // 20. elseif chains
        AddHeading(body, "20. elseif Chains");
        AddParagraph(body, "{{#if Quantity >= 100}}");
        AddParagraph(body, "  🏭 Order size: ENTERPRISE (100+ units)");
        AddParagraph(body, "{{#elseif Quantity >= 10}}");
        AddParagraph(body, "  📦 Order size: BULK (10-99 units)");
        AddParagraph(body, "{{#elseif Quantity > 0}}");
        AddParagraph(body, "  🛒 Order size: SMALL (1-9 units)");
        AddParagraph(body, "{{#else}}");
        AddParagraph(body, "  ∅ Order size: EMPTY");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // 21. Membership, string and existence operators, grouping
        AddHeading(body, "21. in / contains / exists / is empty / Grouping");
        AddParagraph(body, "{{#if Customer.Address.Country in (\"Germany\", \"Austria\", \"Switzerland\")}}");
        AddParagraph(body, "  🏔️ DACH region customer (in)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "{{#if Currency in SupportedCurrencies}}");
        AddParagraph(body, "  💶 Currency {{Currency}} is supported (in collection)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "{{#if Customer.Email contains \"@acme.com\"}}");
        AddParagraph(body, "  🏢 Acme corporate account (contains)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "{{#if Notes exists}}");
        AddParagraph(body, "  📝 Notes field is present (exists)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "{{#if Notes is empty}}");
        AddParagraph(body, "  ∅ No notes were entered (is empty)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "{{#if Features is not empty}}");
        AddParagraph(body, "  ✨ Product has features (is not empty)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "{{#if (IsVIPCustomer or Quantity >= 100) and not IsPaid}}");
        AddParagraph(body, "  📞 Call VIP/large customer about outstanding payment (grouping)");
        AddParagraph(body, "{{/if}}");
        AddParagraph(body, "");

        // Footer
        AddParagraph(body, "");
        AddParagraph(body, "────────────────────────────────────────");
        AddParagraph(body, "End of Template Demo");

        mainPart.Document.Save();
    }

    private static Dictionary<string, object> CreateComprehensiveTestData()
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
            ["SupportedCurrencies"] = new List<string> { "EUR", "CHF", "USD" },
            ["Notes"] = "",

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

    private static ProcessingResult ProcessTemplate(string templatePath, string outputPath, Dictionary<string, object> data)
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
}
