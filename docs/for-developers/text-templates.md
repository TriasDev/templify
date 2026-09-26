# Text Template Processing

Templify provides a `TextTemplateProcessor` for processing text templates using the same familiar syntax as Word document templates. This is ideal for generating emails, notifications, SMS messages, and other text-based content.

## Overview

The `TextTemplateProcessor` class processes plain text templates with the same template syntax as Word document templates:
- Placeholder replacement, including format specifiers (`{{Amount:currency}}`, `{{Flag:yesno}}`, ...)
- Inline expressions (`{{(Count > 0 and IsActive)}}`)
- Conditional blocks with `{{#if}}`, `{{#elseif}}` and `{{#else}}`
- Loops, including named iteration variables (`{{#foreach item in Items}}`)
- Nested structures (any depth, no limit on the number of blocks)
- Loop metadata
- `TextReplacements` applied to replaced values
- Processing warnings (`TextProcessingResult.Warnings`)

Markers are case-insensitive (`{{#IF}}`, `{{/ForEach}}`), as in Word templates. Word-only features are not available: formatting and markdown (markdown characters in values are inserted verbatim; `:raw` is accepted and simply means "no format"), table rows, `UpdateFieldsOnOpen` and `DocumentProperties`.

Unlike Word templates, where loop markers must be in their own paragraphs, markers can appear anywhere in the
text, also inline (`Tags: {{#foreach Tags}}[{{.}}]{{/foreach}}`). The text is kept exactly as written around
the markers, including line breaks: a marker on its own line leaves that line's line break in the output.

**Key Benefits:**
- ✅ Same template syntax as Word documents
- ✅ Reusable data structures across document and text templates
- ✅ No Word document involved: works on plain strings
- ✅ Ideal for email generation, notifications, and dynamic content
- ✅ Single linear scan of the template, no limit on the number or nesting of blocks

## Quick Start

### Basic Usage

```csharp
using TriasDev.Templify.Core;

var processor = new TextTemplateProcessor();

string template = "Hello {{Name}}, welcome to {{CompanyName}}!";

var data = new Dictionary<string, object>
{
    ["Name"] = "Alice Smith",
    ["CompanyName"] = "TriasDev"
};

TextProcessingResult result = processor.ProcessTemplate(template, data);

if (result.IsSuccess)
{
    Console.WriteLine(result.ProcessedText);
    // Output: Hello Alice Smith, welcome to TriasDev!
}
```

### Email Generation Example

```csharp
using System.Globalization;
using TriasDev.Templify.Core;

var processor = new TextTemplateProcessor(new PlaceholderReplacementOptions
{
    Culture = CultureInfo.InvariantCulture
});

string emailTemplate = @"Dear {{CustomerName}},

Thank you for your order #{{OrderId}}.

{{#if IsVip}}As a VIP customer, you'll receive free shipping!{{#else}}Your order will arrive in 3-5 business days.{{/if}}

Order Details:
{{#foreach Items}}- {{Name}}: ${{Price}}
{{/foreach}}
Total: ${{Total}}

Best regards,
The {{CompanyName}} Team";

var data = new Dictionary<string, object>
{
    ["CustomerName"] = "Alice Smith",
    ["OrderId"] = 12345,
    ["IsVip"] = true,
    ["CompanyName"] = "TriasDev",
    ["Items"] = new[]
    {
        new { Name = "Premium Widget", Price = 29.99 },
        new { Name = "Deluxe Gadget", Price = 49.99 }
    },
    ["Total"] = 79.98
};

var result = processor.ProcessTemplate(emailTemplate, data);

if (result.IsSuccess)
{
    // Send email with result.ProcessedText
    SendEmail(to: "alice@example.com", body: result.ProcessedText);
}
```

**Output:**
```text
Dear Alice Smith,

Thank you for your order #12345.

As a VIP customer, you'll receive free shipping!

Order Details:
- Premium Widget: $29.99
- Deluxe Gadget: $49.99

Total: $79.98

Best regards,
The TriasDev Team
```

### Line Breaks Around Markers

The text around markers is kept exactly as written. A marker on a line of its own therefore leaves an empty line
behind (the line break after it stays in the output). To avoid that, start the repeated or conditional text right
after the opening marker and end it with the line break before the closing marker, as in the example above:

```text
{{#foreach Items}}- {{Name}}
{{/foreach}}
```

## Features

### 1. Placeholders

Replace simple and nested placeholders with data values.

```csharp
string template = @"
Name: {{Name}}
Email: {{Contact.Email}}
City: {{Contact.Address.City}}
First Item: {{Items[0]}}
";

var data = new Dictionary<string, object>
{
    ["Name"] = "Bob",
    ["Contact"] = new
    {
        Email = "bob@example.com",
        Address = new { City = "New York" }
    },
    ["Items"] = new[] { "Widget", "Gadget" }
};
```

### 2. Conditionals

Use conditional blocks to show/hide content based on data.

```csharp
string template = @"
Hi {{Name}},
{{#if HasDiscount}}
Good news! You have a {{DiscountPercent}}% discount available.
{{#elseif IsNewCustomer}}
Welcome! Enjoy 10% off your first order.
{{#else}}
Shop now and get great deals!
{{/if}}
";

var data = new Dictionary<string, object>
{
    ["Name"] = "Alice",
    ["HasDiscount"] = true,
    ["DiscountPercent"] = 20
};
```

**Supported Operators:** the full condition syntax of Word templates (same truthiness, precedence and reserved-word rules), see [Condition Evaluation](condition-evaluation.md):
- Comparison: `=`, `!=`, `>`, `<`, `>=`, `<=`
- Logical: `and`, `or`, `not`, parentheses
- Membership, string and existence checks: `in`, `contains`, `startswith`, `endswith`, `exists`, `is empty`, `is not empty`

```csharp
// Examples:
"{{#if Status = \"Active\"}}"
"{{#if Count > 0}}"
"{{#if IsEnabled and not IsExpired}}"
"{{#if Role in (\"Admin\", \"Editor\")}}"
"{{#if Notes is not empty}}"
```

A condition that cannot be parsed (e.g. `{{#if A && B}}`; use `and`) is treated as false and reported as an `ExpressionFailed` warning.

**Inline expressions** evaluate a condition in place and print the result (combine with a boolean format):

```csharp
"Active: {{(IsActive and not IsExpired):yesno}}"   // Active: Yes
```

Inline expressions use the stricter inline dialect (only boolean `true` is true, single-quoted strings allowed),
as in Word templates; see [Inline Expressions Use a Stricter Dialect](condition-evaluation.md#inline-expressions-use-a-stricter-dialect).

### 3. Loops

Iterate over collections with full support for nested data.

```csharp
string template = @"Tasks for today:
{{#foreach Tasks}}{{@number}}. {{Title}} - Priority: {{Priority}}
{{/foreach}}";

var data = new Dictionary<string, object>
{
    ["Tasks"] = new[]
    {
        new { Title = "Review PR", Priority = "High" },
        new { Title = "Update docs", Priority = "Medium" },
        new { Title = "Fix bug", Priority = "High" }
    }
};
```

**Output:**
```
Tasks for today:
1. Review PR - Priority: High
2. Update docs - Priority: Medium
3. Fix bug - Priority: High
```

**Named iteration variables** give access to outer loop items in nested loops:

```csharp
string template = @"
{{#foreach category in Categories}}
{{#foreach product in category.Products}}
- {{category.Name}}: {{product.Name}}
{{/foreach}}
{{/foreach}}
";
```

A missing collection renders nothing and adds a `MissingLoopCollection` warning (and the name to `MissingVariables`); a `null` collection renders nothing and adds a `NullLoopCollection` warning. Looping over a value that is not a collection (including a string) fails the processing.

### 4. Loop Metadata

Access loop metadata using special placeholders:

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{{@index}}` | Zero-based index | 0, 1, 2, ... |
| `{{@number}}` | One-based number (`@index + 1`) | 1, 2, 3, ... |
| `{{@first}}` | True if first item | True, False |
| `{{@last}}` | True if last item | False, True |
| `{{@count}}` | Total item count | 3 |

```csharp
string template = @"
{{#foreach Items}}
{{Name}}{{#if @last}} (last item){{/if}}
{{/foreach}}
";
```

### 5. Nested Structures

Combine conditionals and loops at any depth. As in Word templates, a conditional is evaluated before its content: loops and placeholders in a branch that is not taken are never evaluated (so they cannot fail or be reported as missing), and conditionals inside a loop are evaluated for each item.

```csharp
string template = @"
{{#if HasOrders}}
Your orders:
{{#foreach Orders}}
  Order #{{OrderId}}:
  {{#foreach Items}}
  - {{Name}}: ${{Price}}
  {{/foreach}}
{{/foreach}}
{{#else}}
You have no orders yet.
{{/if}}
";
```

## Configuration Options

### Culture-Specific Formatting

Control number and date formatting with culture settings:

```csharp
using System.Globalization;

var options = new PlaceholderReplacementOptions
{
    Culture = CultureInfo.InvariantCulture  // or CultureInfo.GetCultureInfo("de-DE")
};

var processor = new TextTemplateProcessor(options);
```

**Example:**
```csharp
var data = new Dictionary<string, object>
{
    ["Price"] = 1234.56,
    ["Date"] = DateTime.Now
};

// With InvariantCulture: 1234.56
// With de-DE: 1234,56
```

### Missing Variable Behavior

Configure how missing variables are handled:

```csharp
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
    // Options:
    // - LeaveUnchanged (default): Keeps {{placeholder}}
    // - ReplaceWithEmpty: Removes placeholder
    // - ThrowException: Throws InvalidOperationException
};

var processor = new TextTemplateProcessor(options);
```

### Text Replacements

`TextReplacements` are applied to every replaced value (not to the template text), as in Word templates:

```csharp
var options = new PlaceholderReplacementOptions
{
    TextReplacements = TextReplacements.HtmlEntities   // e.g. "&amp;" -> "&"
};
```

## Result Handling

The `TextProcessingResult` class provides detailed information about the processing:

```csharp
TextProcessingResult result = processor.ProcessTemplate(template, data);

if (result.IsSuccess)
{
    // Success
    string output = result.ProcessedText;
    int replacements = result.ReplacementCount;

    // Check for missing variables
    if (result.MissingVariables.Any())
    {
        Console.WriteLine($"Warning: Missing variables: {string.Join(", ", result.MissingVariables)}");
    }

    // Non-fatal issues (missing variables/collections, null collections, invalid expressions)
    foreach (ProcessingWarning warning in result.Warnings)
    {
        Console.WriteLine($"[{warning.Type}] {warning.Message}");
    }
}
else
{
    // Failure (syntax errors, etc.)
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsSuccess` | `bool` | True if processing succeeded |
| `ProcessedText` | `string` | The processed output text |
| `ReplacementCount` | `int` | Number of placeholders replaced |
| `ErrorMessage` | `string?` | Error message if processing failed |
| `MissingVariables` | `IReadOnlyList<string>` | List of missing variable names |
| `Warnings` | `IReadOnlyList<ProcessingWarning>` | Non-fatal issues, same types as for Word templates (see [Processing Warnings](processing-warnings.md)) |
| `HasWarnings` | `bool` | True if any warnings were collected |

## Common Use Cases

### 1. Email Notifications

```csharp
// Welcome email
string template = @"
Hi {{FirstName}},

Welcome to {{AppName}}! Your account has been created successfully.

{{#if NeedsEmailVerification}}
Please verify your email by clicking the link below:
{{VerificationUrl}}
{{/if}}

Best regards,
The {{CompanyName}} Team
";
```

### 2. Order Confirmations

```csharp
string template = @"
Order Confirmation #{{OrderNumber}}

Hi {{CustomerName}},

Your order has been confirmed.

Items:
{{#foreach LineItems}}
- {{Quantity}}x {{ProductName}}: ${{LineTotal}}
{{/foreach}}

Subtotal: ${{Subtotal}}
{{#if HasDiscount}}
Discount ({{DiscountPercent}}%): -${{DiscountAmount}}
{{/if}}
Tax: ${{Tax}}
Total: ${{Total}}

Estimated delivery: {{DeliveryDate}}
";
```

### 3. System Notifications

```csharp
string template = @"
System Alert: {{AlertType}}

{{#if IsCritical}}
⚠️ CRITICAL ALERT
{{/if}}

Message: {{Message}}

Affected Services:
{{#foreach AffectedServices}}
- {{Name}} (Status: {{Status}})
{{/foreach}}

Time: {{Timestamp}}
";
```

### 4. Report Summaries

```csharp
string template = @"
Weekly Report - {{WeekOf}}

Summary:
- Total Sales: ${{TotalSales}}
- Orders: {{OrderCount}}
- New Customers: {{NewCustomerCount}}

Top Products:
{{#foreach TopProducts}}
{{@index}}. {{Name}} - {{UnitsSold}} units
{{/foreach}}

{{#if HasAlerts}}
Alerts:
{{#foreach Alerts}}
- {{Message}}
{{/foreach}}
{{/if}}
";
```

### 5. SMS Messages

```csharp
string template = @"
Hi {{Name}}, your appointment at {{Location}} is confirmed for {{Date}} at {{Time}}.
{{#if NeedsConfirmation}}
Reply YES to confirm.
{{/if}}
";
```

## Error Handling

### Template Syntax Errors

Syntax errors (unmatched or misnested `{{#if}}`/`{{#foreach}}`, malformed markers, `{{#elseif}}` after `{{#else}}`, invalid iteration variable names) and data errors (looping over a non-collection) are returned as failure results. Closing or branch markers outside any block (e.g. a stray `{{/if}}`) are kept as literal text.

```csharp
// Missing closing tag
string template = "{{#if IsActive}}Content";

var result = processor.ProcessTemplate(template, data);

if (!result.IsSuccess)
{
    Console.WriteLine(result.ErrorMessage);
    // Output: Processing failed: Unmatched {{#if}} tag at position 0
}
```

### Missing Variable Exceptions

With `ThrowException` behavior:

```csharp
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ThrowException
};

var processor = new TextTemplateProcessor(options);

try
{
    var result = processor.ProcessTemplate("Hello {{Name}}", new Dictionary<string, object>());
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
    // Output: Missing variable: Name
}
```

## Performance Considerations

### Best Practices

1. **Reuse processor instances** - Create once, use multiple times
```csharp
// Good: Reuse processor
var processor = new TextTemplateProcessor();
foreach (var customer in customers)
{
    var result = processor.ProcessTemplate(template, customer.Data);
}
```

2. **Batch processing** - Process multiple templates in parallel for better throughput
```csharp
var results = await Task.WhenAll(
    customers.Select(c => Task.Run(() =>
        processor.ProcessTemplate(template, c.Data)))
);
```

### Performance Characteristics

- **Scaling**: The template is parsed in a single linear scan; there is no limit on the number or nesting of blocks
- **Thread safety**: A `TextTemplateProcessor` and its options can be shared by concurrent calls
- **Conditions**: Parsed condition expressions are cached

## Comparison with Word Templates

| Feature | TextTemplateProcessor | DocumentTemplateProcessor |
|---------|----------------------|---------------------------|
| Placeholder syntax and format specifiers | ✅ Same | ✅ Same |
| Inline expressions `{{(...)}}` | ✅ Same | ✅ Same |
| Conditionals (`#if`/`#elseif`/`#else`) | ✅ Same | ✅ Same |
| Loops (incl. `item in Items`) | ✅ Same | ✅ Same |
| Nested structures | ✅ Same | ✅ Same |
| Warnings | ✅ Same | ✅ Same |
| `TextReplacements` | ✅ Same | ✅ Same |
| Formatting preservation / markdown | ❌ Plain text | ✅ Rich formatting |
| Tables | ❌ N/A | ✅ Supported |
| Loop/condition markers | Anywhere, also inline | Own paragraphs or table rows (inline conditionals allowed) |
| Use case | Emails, SMS, text | Reports, contracts, invoices |

## Advanced Examples

### Multi-Language Templates

```csharp
var templates = new Dictionary<string, string>
{
    ["en"] = "Hello {{Name}}, your order #{{OrderId}} is ready.",
    ["de"] = "Hallo {{Name}}, Ihre Bestellung #{{OrderId}} ist bereit.",
    ["fr"] = "Bonjour {{Name}}, votre commande #{{OrderId}} est prête."
};

var processor = new TextTemplateProcessor();
string template = templates[userLanguage];
var result = processor.ProcessTemplate(template, orderData);
```

### Dynamic Template Loading

```csharp
// Load templates from database or file system
string template = await templateRepository.GetByNameAsync("order-confirmation");

var processor = new TextTemplateProcessor();
var result = processor.ProcessTemplate(template, orderData);
```

### Template Composition

```csharp
// Header template
string header = @"
{{CompanyName}}
{{CompanyAddress}}
---
";

// Body template
string bodyTemplate = @"
Dear {{RecipientName}},

Thank you for your order #{{OrderId}}.

{{#foreach Items}}
- {{Name}}: {{Quantity}}
{{/foreach}}

Best regards,
{{CompanyName}}
";

// Footer template
string footer = @"
---
© {{Year}} {{CompanyName}}
";

// Compose full template
string fullTemplate = header + bodyTemplate + footer;
```

## Migration from Other Systems

### From String.Format

**Before:**
```csharp
string message = string.Format(
    "Hello {0}, your order #{1} is ready.",
    customerName,
    orderId
);
```

**After:**
```csharp
var processor = new TextTemplateProcessor();
var result = processor.ProcessTemplate(
    "Hello {{Name}}, your order #{{OrderId}} is ready.",
    new Dictionary<string, object>
    {
        ["Name"] = customerName,
        ["OrderId"] = orderId
    }
);
```

### From Template Engines (Handlebars, Mustache)

The syntax is very similar, making migration straightforward:

**Handlebars/Mustache:**
```handlebars
Hello {{name}}
{{#if isVip}}
  VIP content
{{/if}}
{{#each items}}
  - {{name}}
{{/each}}
```

**Templify:**
```
Hello {{Name}}
{{#if IsVip}}
  VIP content
{{/if}}
{{#foreach Items}}
  - {{Name}}
{{/foreach}}
```

## See Also

- [Quick Start Guide](quick-start.md) - Getting started with Templify
- [Condition Evaluation](condition-evaluation.md) - Advanced conditional expressions
- [Word Document Templates](../for-template-authors/template-syntax.md) - Creating Word templates
- [Processing Warnings](processing-warnings.md) - Warning types and reports

## Support

- 📖 [Documentation](https://triasdev.github.io/templify/)
- 💬 [GitHub Discussions](https://github.com/TriasDev/templify/discussions)
- 🐛 [Issue Tracker](https://github.com/TriasDev/templify/issues)
