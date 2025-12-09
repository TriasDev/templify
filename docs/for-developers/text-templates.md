# Text Template Processing

Templify provides a `TextTemplateProcessor` for processing text templates using the same familiar syntax as Word document templates. This is ideal for generating emails, notifications, SMS messages, and other text-based content.

## Overview

The `TextTemplateProcessor` class processes plain text templates with the same powerful features available in Word document templates:
- Placeholder replacement
- Conditional blocks
- Loops and iterations
- Nested structures
- Loop metadata

**Key Benefits:**
- ✅ Same template syntax as Word documents
- ✅ Reusable data structures across document and text templates
- ✅ No dependencies on Word or OpenXML for text processing
- ✅ Ideal for email generation, notifications, and dynamic content
- ✅ High performance with simple string manipulation

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
var processor = new TextTemplateProcessor();

string emailTemplate = @"Dear {{CustomerName}},

Thank you for your order #{{OrderId}}.

{{#if IsVip}}
As a VIP customer, you'll receive free shipping!
{{else}}
Your order will arrive in 3-5 business days.
{{/if}}

Order Details:
{{#foreach Items}}
- {{Name}}: ${{Price}}
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
```
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
{{else}}
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

**Supported Operators:**
- Comparison: `=`, `!=`, `>`, `<`, `>=`, `<=`
- Logical: `and`, `or`, `not`

```csharp
// Examples:
"{{#if Status = Active}}"
"{{#if Count > 0}}"
"{{#if IsEnabled and not IsExpired}}"
"{{#if Score >= 80 or IsPremium}}"
```

### 3. Loops

Iterate over collections with full support for nested data.

```csharp
string template = @"
Tasks for today:
{{#foreach Tasks}}
{{@index}}. {{Title}} - Priority: {{Priority}}
{{/foreach}}
";

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
0. Review PR - Priority: High
1. Update docs - Priority: Medium
2. Fix bug - Priority: High
```

### 4. Loop Metadata

Access loop metadata using special placeholders:

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{{@index}}` | Zero-based index | 0, 1, 2, ... |
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

Combine conditionals and loops at any depth.

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
{{else}}
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

Syntax errors are returned as failure results:

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

2. **Pre-compile templates** - For high-frequency processing, consider caching templates

3. **Batch processing** - Process multiple templates in parallel for better throughput
```csharp
var results = await Task.WhenAll(
    customers.Select(c => Task.Run(() =>
        processor.ProcessTemplate(template, c.Data)))
);
```

### Performance Characteristics

- **Simple placeholders**: ~0.1ms for 10 placeholders
- **Conditionals**: ~0.2ms per conditional block
- **Loops**: ~0.1ms per iteration + nested content processing
- **Memory**: Minimal allocation, efficient string building

## Comparison with Word Templates

| Feature | TextTemplateProcessor | DocumentTemplateProcessor |
|---------|----------------------|---------------------------|
| Placeholder syntax | ✅ Same | ✅ Same |
| Conditionals | ✅ Same | ✅ Same |
| Loops | ✅ Same | ✅ Same |
| Nested structures | ✅ Same | ✅ Same |
| Formatting preservation | ❌ Plain text | ✅ Rich formatting |
| Tables | ❌ N/A | ✅ Supported |
| Dependencies | ✅ None | OpenXML SDK |
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
- [Word Document Templates](../for-template-authors/) - Creating Word templates
- [API Reference](../api/) - Complete API documentation

## Support

- 📖 [Documentation](https://triasdev.github.io/templify/)
- 💬 [GitHub Discussions](https://github.com/TriasDev/templify/discussions)
- 🐛 [Issue Tracker](https://github.com/TriasDev/templify/issues)
- 📧 Email: support@triasdev.com
