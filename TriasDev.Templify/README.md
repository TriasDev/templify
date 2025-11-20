# Templify

A simple, focused .NET library for replacing placeholders in Word documents (.docx) without requiring Microsoft Word to be installed.

## Overview

Templify is built on the Microsoft OpenXML SDK and provides a straightforward API for variable replacement in Word document templates. Unlike complex templating systems, this library focuses on the most common use case: replacing `{{placeholders}}` with actual values.

## Features

- **Simple placeholder syntax**: Use `{{variableName}}` in your Word templates
- **Boolean format specifiers**: Display booleans as checkboxes (☑/☐), Yes/No, checkmarks (✓/✗), and more
- **Boolean expressions**: Evaluate logic in placeholders with `and`, `or`, `not`, and comparison operators
- **Conditional blocks**: Use `{{#if condition}}...{{else}}...{{/if}}` for dynamic content
- **Advanced conditions**: Support for `eq`, `ne`, `gt`, `lt`, `gte`, `lte`, `and`, `or`, `not` operators
- **Loops and iterations**: Use `{{#foreach}}...{{/foreach}}` to repeat content for collections
- **Loop metadata**: Access index, first/last flags, and count within loops
- **Formatting preservation**: Bold, italic, fonts, colors, and styles are automatically preserved
- **Nested data structures**: Access nested objects with dot notation and array indexing
- **Type-safe data binding**: Pass values via `Dictionary<string, object>` or JSON
- **Smart value conversion**: Automatically converts numbers, dates, booleans to readable strings
- **Localization support**: Format specifiers adapt to cultures (en, de, fr, es, it, pt)
- **Table support**: Replace placeholders in table cells, repeat table rows, conditional table rows
- **Configurable behavior**: Control what happens when variables are missing
- **No Word required**: Pure OpenXML processing, no COM automation
- **.NET 9**: Built with modern .NET features

## Quick Start

### Installation

Add the package reference to your project:

```bash
dotnet add package TriasDev.Templify
```

### Basic Usage

```csharp
using TriasDev.Templify;

// Prepare your data
var data = new Dictionary<string, object>
{
    ["CompanyName"] = "TriasDev GmbH & Co. KG",
    ["ContactPerson"] = "Max Mustermann",
    ["Date"] = DateTime.Now,
    ["Amount"] = 1250.50m,
    ["IsApproved"] = true
};

// Create processor with default options
var processor = new DocumentTemplateProcessor();

// Process template
using var templateStream = File.OpenRead("template.docx");
using var outputStream = File.Create("output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.IsSuccess)
{
    Console.WriteLine($"Processed successfully! Replaced {result.ReplacementCount} placeholders.");
}
```

### Word Template Example

Create a Word document with placeholders:

```
Invoice

Company: {{CompanyName}}
Contact: {{ContactPerson}}
Date: {{Date}}
Amount: {{Amount}} EUR
Status: {{IsApproved}}
```

After processing, the output will contain:

```
Invoice

Company: TriasDev GmbH & Co. KG
Contact: Max Mustermann
Date: 11/7/2025 10:30:00 AM
Amount: 1250.50 EUR
Status: True
```

## Nested Data Structures

Templify supports accessing nested properties, collections, and dictionaries using mixed notation.

### Supported Syntax

- **Dot notation**: `{{Customer.Address.City}}`
- **Array/collection indexing**: `{{Items[0].Name}}`
- **Dictionary access**: `{{Settings[Theme]}` or `{{Settings.Theme}}`
- **Mixed notation**: `{{Orders[0].Customer.Address.City}}`

### Nested Objects Example

```csharp
var data = new Dictionary<string, object>
{
    ["Invoice"] = new Invoice
    {
        Number = "INV-2025-001",
        Customer = new Customer
        {
            Name = "TriasDev GmbH & Co. KG",
            Address = new Address
            {
                Street = "Tech Street 123",
                City = "Munich",
                PostalCode = "80331"
            }
        },
        TotalAmount = 2499.99m
    }
};

// Template can now use:
// {{Invoice.Number}}
// {{Invoice.Customer.Name}}
// {{Invoice.Customer.Address.City}}
// {{Invoice.TotalAmount}}
```

### Collections and Arrays

```csharp
var data = new Dictionary<string, object>
{
    ["CompanyName"] = "TriasDev GmbH & Co. KG",
    ["Items"] = new List<string> { "License", "Support", "Training" },
    ["Orders"] = new List<Order>
    {
        new Order { Id = "ORD-001", Amount = 999.00m },
        new Order { Id = "ORD-002", Amount = 1500.00m }
    }
};

// Template can use:
// {{Items[0]}}  → "License"
// {{Items[1]}}  → "Support"
// {{Orders[0].Id}}  → "ORD-001"
// {{Orders[1].Amount}}  → "1500.00"
```

### Dictionaries

```csharp
var data = new Dictionary<string, object>
{
    ["Settings"] = new Dictionary<string, string>
    {
        ["Theme"] = "Dark",
        ["Language"] = "German",
        ["Currency"] = "EUR"
    }
};

// Both syntaxes work:
// {{Settings[Theme]}}  → "Dark"
// {{Settings.Theme}}   → "Dark"
```

### Real-World Example: Invoice with Line Items

```csharp
var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = "INV-2025-001",
    ["Company"] = new Company
    {
        Name = "TriasDev GmbH & Co. KG",
        Address = "Tech Street 123, 80331 Munich"
    },
    ["LineItems"] = new List<LineItem>
    {
        new LineItem { Product = "Enterprise Edition", Quantity = 5, Price = 499.00m },
        new LineItem { Product = "Annual Support", Quantity = 5, Price = 99.00m },
        new LineItem { Product = "Training Package", Quantity = 2, Price = 250.00m }
    },
    ["Subtotal"] = 3485.00m,
    ["Tax"] = 661.95m,
    ["Total"] = 4146.95m
};

// Word template:
// Invoice: {{InvoiceNumber}}
// Customer: {{Company.Name}}
// Address: {{Company.Address}}
//
// Line Items:
// 1. {{LineItems[0].Product}} - Qty: {{LineItems[0].Quantity}} @ {{LineItems[0].Price}} EUR
// 2. {{LineItems[1].Product}} - Qty: {{LineItems[1].Quantity}} @ {{LineItems[1].Price}} EUR
// 3. {{LineItems[2].Product}} - Qty: {{LineItems[2].Quantity}} @ {{LineItems[2].Price}} EUR
//
// Subtotal: {{Subtotal}} EUR
// Tax (19%): {{Tax}} EUR
// Total: {{Total}} EUR
```

### Backward Compatibility

Simple placeholders (without dots or brackets) work exactly as before:

```csharp
var data = new Dictionary<string, object>
{
    ["Name"] = "John Doe",
    ["Age"] = 30
};

// {{Name}} and {{Age}} work as expected
```

Direct dictionary keys take precedence over nested paths:

```csharp
var data = new Dictionary<string, object>
{
    ["Customer.Name"] = "Direct Value",  // This key is checked first
    ["Customer"] = new Customer { Name = "Nested Value" }
};

// {{Customer.Name}} returns "Direct Value" (fast path)
```

## Conditional Blocks

Templify supports conditional content using `{{#if condition}}...{{else}}...{{/if}}` syntax. Show or hide content based on data values and complex expressions.

### Basic Conditional Syntax

```
{{#if VariableName}}
  Content shown when condition is true
{{/if}}

{{#if VariableName}}
  Content when true
{{else}}
  Content when false
{{/if}}
```

### Boolean Evaluation Rules

- `true`/`false` → boolean value
- `1`/`0` → true/false
- `"true"`/`"false"` (string) → true/false
- Non-empty string → true
- Empty string/whitespace → false
- Non-empty collection → true
- Empty collection → false
- `null` → false

### Comparison Operators

```
{{#if Status = "Active"}}         Equal to
{{#if Status != "Deleted"}}        Not equal to
{{#if Count > 0}}                 Greater than
{{#if Count < 100}}               Less than
{{#if Price >= 100}}              Greater or equal
{{#if Price <= 1000}}             Less or equal
```

### Logical Operators

```
{{#if IsActive and Count > 0}}           AND
{{#if Status = "Active" or Status = "Pending"}}    OR
{{#if not IsDeleted}}                     NOT
```

### Complex Expressions

```
{{#if Price > 100 and Price < 1000}}
  Mid-range product
{{/if}}

{{#if Status = "Active" or Status = "Pending"}}
  In progress
{{else}}
  Completed or cancelled
{{/if}}
```

### Quoted Strings (with Spaces)

```
{{#if Status = "In Progress"}}
  Work in progress...
{{/if}}

{{#if Customer.Address.Country = "United States"}}
  US customer
{{/if}}
```

### Nested Properties

```
{{#if Customer.Address.Country = "Germany"}}
  German customer - {{Customer.Name}}
{{/if}}

{{#if Order.LineItems[0].Price > 1000}}
  High-value first item
{{/if}}
```

### Real-World Example: Conditional Sections

**Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["HasDiscount"] = true,
    ["DiscountPercent"] = 15,
    ["IsVIPCustomer"] = true,
    ["OrderTotal"] = 2500.00m,
    ["Status"] = "Active",
    ["Country"] = "Germany"
};
```

**Word Template:**
```
Order Status: {{Status}}

{{#if HasDiscount}}
Special Discount: {{DiscountPercent}}% off!
{{/if}}

{{#if IsVIPCustomer and OrderTotal > 1000}}
VIP Customer - Free expedited shipping included!
{{/if}}

{{#if Country = "Germany"}}
Shipping: 5-7 business days within Germany
{{else}}
Shipping: International delivery 10-14 days
{{/if}}
```

**Output:**
```
Order Status: Active

Special Discount: 15% off!

VIP Customer - Free expedited shipping included!

Shipping: 5-7 business days within Germany
```

### Nested Conditionals

Templify fully supports nested conditionals, allowing you to create complex branching logic:

**Example: Multi-Level Decision Tree**

**Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["IsVIP"] = true,
    ["HasActiveSubscription"] = true,
    ["SubscriptionTier"] = "Premium",
    ["OrderTotal"] = 1500.00m
};
```

**Word Template:**
```
{{#if IsVIP}}
VIP Customer Benefits:

{{#if HasActiveSubscription}}
  Active Subscription: {{SubscriptionTier}}

  {{#if SubscriptionTier = "Premium"}}
    - Priority Support (24/7)
    - Free Shipping on all orders
    - 20% discount on all purchases
  {{else}}
    - Standard Support
    - Free Shipping on orders over $100
    - 10% discount on all purchases
  {{/if}}
{{else}}
  No active subscription. Consider upgrading!
{{/if}}

{{#if OrderTotal > 1000}}
  High-value order - Personal Account Manager assigned
{{/if}}
{{else}}
Regular Customer
- Standard shipping rates apply
- 5% discount on orders over $200
{{/if}}
```

**Output:**
```
VIP Customer Benefits:

  Active Subscription: Premium

    - Priority Support (24/7)
    - Free Shipping on all orders
    - 20% discount on all purchases

  High-value order - Personal Account Manager assigned
```

**Nesting Rules:**
- Conditionals can be nested to any depth
- Inner conditionals are evaluated first, then outer conditionals
- Each `{{#if}}` must have a matching `{{/if}}`
- `{{else}}` can also contain nested conditionals

## Loops and Iterations

Templify supports repeating content for collections using the `{{#foreach}}...{{/foreach}}` syntax. Loops work with both paragraphs and table rows.

### Basic Loop Syntax

```
{{#foreach CollectionName}}
  Content to repeat for each item
{{/foreach}}
```

### Loop Example with List of Objects

**Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = "INV-2025-001",
    ["LineItems"] = new List<LineItem>
    {
        new LineItem { Position = 1, Product = "Software License", Quantity = 5, UnitPrice = 499.00m, Total = 2495.00m },
        new LineItem { Position = 2, Product = "Support & Maintenance", Quantity = 5, UnitPrice = 99.00m, Total = 495.00m },
        new LineItem { Position = 3, Product = "Training Package", Quantity = 2, UnitPrice = 250.00m, Total = 500.00m }
    },
    ["Subtotal"] = 3490.00m,
    ["Tax"] = 663.10m,
    ["Total"] = 4153.10m
};
```

**Word Template:**
```
Invoice: {{InvoiceNumber}}

Line Items:
{{#foreach LineItems}}
{{Position}}. {{Product}}
   Quantity: {{Quantity}} @ {{UnitPrice}} EUR = {{Total}} EUR
{{/foreach}}

Subtotal: {{Subtotal}} EUR
Tax (19%): {{Tax}} EUR
Total: {{Total}} EUR
```

**Output:**
```
Invoice: INV-2025-001

Line Items:
1. Software License
   Quantity: 5 @ 499.00 EUR = 2495.00 EUR
2. Support & Maintenance
   Quantity: 5 @ 99.00 EUR = 495.00 EUR
3. Training Package
   Quantity: 2 @ 250.00 EUR = 500.00 EUR

Subtotal: 3490.00 EUR
Tax (19%): 663.10 EUR
Total: 4153.10 EUR
```

### Loop Metadata Variables

Within a loop, you have access to special metadata variables:

- `{{@index}}` - Zero-based index (0, 1, 2, ...)
- `{{@first}}` - `True` for the first item, `False` otherwise
- `{{@last}}` - `True` for the last item, `False` otherwise
- `{{@count}}` - Total number of items in the collection

**Example using metadata:**
```
{{#foreach Items}}
Item {{@index}}: {{Name}}{{#if @last}} (Last Item){{/if}}
{{/foreach}}
```

### Table Loops

Loops work seamlessly with tables. Place the loop markers in table cells:

| Position | Product | Quantity | Price | Total |
|----------|---------|----------|-------|-------|
| {{#foreach LineItems}} | | | | |
| {{Position}} | {{Product}} | {{Quantity}} | {{UnitPrice}} | {{Total}} |
| {{/foreach}} | | | | |

The row containing the loop markers will be repeated for each item.

### Nested Loops

You can nest loops for hierarchical data:

```csharp
var data = new Dictionary<string, object>
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
        }
    }
};
```

**Template:**
```
{{#foreach Orders}}
Order: {{OrderId}}
  Items:
  {{#foreach Items}}
    - {{Product}} (Qty: {{Quantity}})
  {{/foreach}}
{{/foreach}}
```

### Empty Collections

If a collection is empty, the loop block (including markers) is removed from the output. No special handling required.

### Loop Variable Scope

Within a loop:
1. **Direct property access**: Use `{{PropertyName}}` to access properties of the current item
2. **Primitive values**: Use `{{.}}` or `{{this}}` to reference the current item itself (for collections of strings, numbers, etc.)
3. **Root data access**: Variables from the root data dictionary are still accessible
4. **Nested property access**: Use dot notation for nested properties (e.g., `{{Customer.Name}}`)
5. **Parent loop access**: In nested loops, you can access parent loop variables

### Simple Collections (Strings, Numbers)

For collections of primitive values, use `{{.}}` or `{{this}}` to reference the current item:

```csharp
var data = new Dictionary<string, object>
{
    ["Items"] = new List<string> { "Item One", "Item Two", "Item Three" }
};
```

**Template:**
```
{{#foreach Items}}
- {{.}}
{{/foreach}}
```

**Output:**
```
- Item One
- Item Two
- Item Three
```

## Formatting and Styles

### Automatic Formatting Preservation

Templify automatically preserves all character formatting when replacing placeholders. This means:

- **Bold text** stays bold
- *Italic text* stays italic
- Font family, size, and color are preserved
- Underline, strikethrough, and other character formatting is maintained
- Paragraph styles (Heading 1, Normal, etc.) are preserved

**Example:**

If your template contains:
```
Customer: {{CustomerName}}
```

Where `{{CustomerName}}` is formatted as **Bold, Arial, 14pt, Blue**, the replacement value will also be **Bold, Arial, 14pt, Blue**.

### How It Works

When replacing a placeholder:
1. The library extracts the formatting (RunProperties) from the original placeholder text
2. Clones these properties
3. Applies them to the replacement text

This happens automatically - no configuration needed.

### Formatting in Loops

Formatting is also preserved when cloning content in loops:

```
{{#foreach Items}}
Product: {{Name}}  (if "{{Name}}" is bold in template, it stays bold in output)
Price: {{Price}}
{{/foreach}}
```

All formatting applied to placeholders within loop blocks is preserved for each iteration.

### Lists in Loops

**Bullet lists and numbered lists are fully preserved when used inside `{{#foreach}}` loops.** This allows you to create dynamic lists in your documents.

#### Bullet Lists Example

In your Word template, create a bullet list item with a placeholder:
```
{{#foreach Features}}
• {{.}}
{{/foreach}}
```

**Important:** The bullet (•) should be a real Word bullet list item, not just a bullet character. Use Word's bullet list formatting (Home → Bullets).

```csharp
Dictionary<string, object> data = new Dictionary<string, object>
{
    ["Features"] = new List<string>
    {
        "Advanced compliance tracking",
        "Automated risk assessments",
        "Real-time reporting"
    }
};
```

**Output:** Each feature will be rendered as a properly formatted bullet list item.

#### Numbered Lists Example

Similarly, for numbered lists:
```
Setup Steps:
{{#foreach Steps}}
1. {{.}}
{{/foreach}}
```

**Important:** Use Word's numbered list formatting (Home → Numbering), not manual numbers.

```csharp
Dictionary<string, object> data = new Dictionary<string, object>
{
    ["Steps"] = new List<string>
    {
        "Download the installer",
        "Run setup wizard",
        "Configure settings"
    }
};
```

**Output:** Each step will be rendered as a properly numbered list item (1., 2., 3., etc.).

#### Lists with Object Properties

You can also use object properties in list items:

```
{{#foreach Products}}
• {{Name}} - {{Price}} EUR
{{/foreach}}
```

```csharp
Dictionary<string, object> data = new Dictionary<string, object>
{
    ["Products"] = new List<Product>
    {
        new Product { Name = "Enterprise License", Price = 999.00m },
        new Product { Name = "Support Package", Price = 299.00m }
    }
};
```

**How it works:** When the template processor encounters a loop, it clones the paragraph including all formatting properties (NumberingProperties for lists). Each iteration creates a new list item with the same formatting.

### Mixed Formatting

If a placeholder has mixed formatting (e.g., part bold, part italic), the formatting from the first character of the placeholder is used for the entire replacement value.

### Custom Styles

You can apply Word's built-in styles or custom styles in your template:
- Apply "Heading 1" style to a paragraph containing `{{Title}}`
- Apply "Emphasis" style to `{{ImportantNote}}`
- The replacement text will inherit the paragraph style

**Tip:** Format your placeholders in the Word template exactly how you want the replacement values to appear.

## Configuration Options

### Missing Variable Behavior

Configure what happens when a placeholder doesn't have a corresponding value:

```csharp
// Option 1: Leave placeholders unchanged (default)
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
};

// Option 2: Replace with empty string
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
};

// Option 3: Throw exception
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ThrowException
};

var processor = new DocumentTemplateProcessor(options);
```

### Culture and Formatting

Templify allows you to control how numbers, dates, and other culture-sensitive values are formatted in your documents. This is particularly important for international documents or when you need consistent formatting across different systems.

```csharp
using System.Globalization;

// Option 1: Use InvariantCulture for consistent, culture-independent formatting (recommended for international documents)
var options = new PlaceholderReplacementOptions
{
    Culture = CultureInfo.InvariantCulture
};
// Numbers: 1250.50, Dates: 11/07/2025 10:30:00

// Option 2: Use specific culture for localized documents (e.g., German invoices)
var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE")
};
// Numbers: 1250,50, Dates: 07.11.2025 10:30:00

// Option 3: Use CurrentCulture for system locale (default behavior)
var options = new PlaceholderReplacementOptions
{
    Culture = CultureInfo.CurrentCulture
};
// Formats according to the system's regional settings

var processor = new DocumentTemplateProcessor(options);
```

**What is affected by culture:**
- **Decimal numbers**: Decimal separator (dot vs comma), thousand separators
- **Dates and times**: Date format, month/day order, time separators
- **Integers and longs**: Thousand separators in some cultures

**What is NOT affected by culture:**
- **Strings**: Always used as-is
- **Booleans**: Always "True" or "False" in English

**Best Practices:**
- Use `CultureInfo.InvariantCulture` for automated tests to ensure consistent results
- Use specific cultures (e.g., `de-DE`, `en-US`) when generating localized business documents
- Use `CurrentCulture` when you want documents to match the user's regional settings
- Consider your audience: international reports should use InvariantCulture, local invoices should use the appropriate regional culture

**Example: Localized German Invoice**
```csharp
var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = "RE-2025-001",
    ["Date"] = DateTime.Now,
    ["Amount"] = 1250.50m
};

var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE")
};

var processor = new DocumentTemplateProcessor(options);
processor.ProcessTemplate(templateStream, outputStream, data);

// Result in document:
// Rechnungsnummer: RE-2025-001
// Datum: 07.11.2025 14:30:00
// Betrag: 1250,50 EUR  (note the comma instead of dot)
```

## Working with Tables

Placeholders in table cells are automatically detected and replaced:

| Product | Price | Status |
|---------|-------|--------|
| {{ProductName}} | {{Price}} | {{Status}} |

```csharp
var data = new Dictionary<string, object>
{
    ["ProductName"] = "Software License",
    ["Price"] = 999.00m,
    ["Status"] = "Active"
};

processor.ProcessTemplate(templateStream, outputStream, data);
```

## Data Type Support

The library automatically converts common .NET types to readable strings:

- **String**: Used as-is
- **Numbers** (int, decimal, double, etc.): Formatted with culture-specific settings
- **DateTime/DateTimeOffset**: Formatted using standard date/time format
- **Boolean**: "True" or "False" (or use format specifiers for custom display)
- **Null**: Empty string or handled based on MissingVariableBehavior
- **Custom objects**: Uses `ToString()` method

## Format Specifiers

Format specifiers allow you to control how boolean values are displayed in your documents. Use the `:format` syntax to transform boolean values into checkboxes, Yes/No text, checkmarks, and more.

### Quick Example

**Template:**
```
Status: {{IsActive:checkbox}}
Verified: {{IsVerified:yesno}}
Valid: {{IsValid:checkmark}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["IsActive"] = true,
    ["IsVerified"] = false,
    ["IsValid"] = true
};
```

**JSON Data:**
```json
{
  "IsActive": true,
  "IsVerified": false,
  "IsValid": true
}
```

**Output:**
```
Status: ☑
Verified: No
Valid: ✓
```

### Available Formatters

| Format | True | False | Use Case |
|--------|------|-------|----------|
| `checkbox` | ☑ | ☐ | Task lists, checklists |
| `yesno` | Yes | No | Questions, confirmations |
| `checkmark` | ✓ | ✗ | Validation, requirements |
| `truefalse` | True | False | Technical output |
| `onoff` | On | Off | Settings, switches |
| `enabled` | Enabled | Disabled | Feature flags |
| `active` | Active | Inactive | Status indicators |

### Localization

Format specifiers automatically adapt to the specified culture:

```csharp
// German output
var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE"),
    BooleanFormatterRegistry = new BooleanFormatterRegistry(new CultureInfo("de-DE"))
};
var processor = new DocumentTemplateProcessor(options);
```

**Template:** `{{IsActive:yesno}}`
**Output (de-DE):** `Ja` or `Nein`
**Output (fr-FR):** `Oui` or `Non`
**Output (es-ES):** `Sí` or `No`

### Custom Formatters

Create your own boolean formatters:

```csharp
var registry = new BooleanFormatterRegistry();
registry.Register("thumbs", new BooleanFormatter("👍", "👎"));

var options = new PlaceholderReplacementOptions
{
    BooleanFormatterRegistry = registry
};
var processor = new DocumentTemplateProcessor(options);
```

**Template:** `{{IsPositive:thumbs}}`
**Output:** `👍` or `👎`

For complete documentation, see the [Format Specifiers Guide](../docs/guides/format-specifiers.md).

## Boolean Expressions

Boolean expressions allow you to evaluate logic directly within placeholders, eliminating the need for separate conditional blocks for simple boolean checks.

### Quick Example

**Template:**
```
Eligible: {{(Age >= 18):yesno}}
Access: {{(IsActive and IsVerified):checkbox}}
Can proceed: {{(HasPermissionA or HasPermissionB):checkmark}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Age"] = 25,
    ["IsActive"] = true,
    ["IsVerified"] = true,
    ["HasPermissionA"] = false,
    ["HasPermissionB"] = true
};
```

**JSON Data:**
```json
{
  "Age": 25,
  "IsActive": true,
  "IsVerified": true,
  "HasPermissionA": false,
  "HasPermissionB": true
}
```

**Output:**
```
Eligible: Yes
Access: ☑
Can proceed: ✓
```

### Operators

**Logical:**
- `and` - Both conditions must be true
- `or` - At least one condition must be true
- `not` - Negates the condition

**Comparison:**
- `==` - Equal to
- `!=` - Not equal to
- `>` - Greater than
- `>=` - Greater than or equal
- `<` - Less than
- `<=` - Less than or equal

### Nested Expressions

Use parentheses to control evaluation order:

**Template:**
```
Approved: {{((Age >= 18) and (HasLicense or HasPermit)):yesno}}
```

**Data:**
```json
{
  "Age": 20,
  "HasLicense": false,
  "HasPermit": true
}
```

**Output:**
```
Approved: Yes
```

### In Loops

Expressions work seamlessly in loops:

**Template:**
```
{{#foreach Employees}}
- {{Name}}: {{(IsActive and HasCompletedTraining):checkbox}}
{{/foreach}}
```

**Data:**
```json
{
  "Employees": [
    { "Name": "Alice", "IsActive": true, "HasCompletedTraining": true },
    { "Name": "Bob", "IsActive": true, "HasCompletedTraining": false }
  ]
}
```

**Output:**
```
- Alice: ☑
- Bob: ☐
```

For complete documentation, see the [Boolean Expressions Guide](../docs/guides/boolean-expressions.md).

## Supported Document Locations

Current MVP supports:
- Document body paragraphs
- Table cells

Future versions may include:
- Headers and footers
- Text boxes
- Footnotes/endnotes
- Custom XML parts

## API Reference

### DocumentTemplateProcessor

Main entry point for template processing.

**Constructor:**
```csharp
DocumentTemplateProcessor(PlaceholderReplacementOptions? options = null)
```

**Methods:**
```csharp
ProcessingResult ProcessTemplate(
    Stream templateStream,
    Stream outputStream,
    Dictionary<string, object> data)
```

### PlaceholderReplacementOptions

Configuration options for template processing.

**Properties:**
- `MissingVariableBehavior`: How to handle missing variables (default: `LeaveUnchanged`)

### ProcessingResult

Result of template processing operation.

**Properties:**
- `IsSuccess`: Whether processing completed successfully
- `ReplacementCount`: Number of placeholders replaced
- `ErrorMessage`: Error details (if IsSuccess is false)
- `MissingVariables`: List of placeholders without values (if any)

## Error Handling

```csharp
try
{
    var result = processor.ProcessTemplate(templateStream, outputStream, data);

    if (!result.IsSuccess)
    {
        Console.WriteLine($"Processing failed: {result.ErrorMessage}");
    }

    if (result.MissingVariables.Any())
    {
        Console.WriteLine($"Warning: Missing variables: {string.Join(", ", result.MissingVariables)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## Requirements

- .NET 9.0 or later
- DocumentFormat.OpenXml 3.3.0 or later

## About

**Templify** is created and maintained by **TriasDev GmbH & Co. KG**.

This library is battle-tested in production, processing thousands of documents daily with enterprise-grade reliability.

## License

*[License information to be added]*

© 2025 TriasDev GmbH & Co. KG

## Contributing

Contributions are welcome! Please see the main repository for contribution guidelines.

## See Also

- [Architecture Documentation](ARCHITECTURE.md) - Design and implementation details
- [Examples](Examples.md) - More code samples and use cases
