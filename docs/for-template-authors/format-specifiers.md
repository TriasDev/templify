# Format Specifiers Guide

Format specifiers allow you to control how values are displayed in your generated documents. You can format booleans as checkboxes or Yes/No text, display numbers with specific decimal places, and format currency values according to locale.

## Table of Contents

- [Quick Start](#quick-start)
- [Available Format Specifiers](#available-format-specifiers)
  - [Boolean Formatters](#boolean-formatters)
  - [Number and Currency Formatters](#number-and-currency-formatters)
- [Using Format Specifiers](#using-format-specifiers)
- [Localization Support](#localization-support)
- [Custom Formatters](#custom-formatters)
- [Advanced Usage](#advanced-usage)
- [Best Practices](#best-practices)

## Quick Start

Add a format specifier to any boolean placeholder using the `:format` syntax:

**Template:**
```
User Status: {{IsActive:checkbox}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["IsActive"] = true
};
```

**JSON Data:**
```json
{
  "IsActive": true
}
```

**Output:**
```
User Status: ☑
```

## Available Format Specifiers

### Boolean Formatters

#### checkbox
Displays a checked or unchecked checkbox symbol.

| Value | Output |
|-------|--------|
| `true` | ☑ |
| `false` | ☐ |

**Example:**
```
Task completed: {{IsCompleted:checkbox}}
```

#### yesno
Displays "Yes" or "No" text.

| Value | Output |
|-------|--------|
| `true` | Yes |
| `false` | No |

**Example:**
```
Approved: {{IsApproved:yesno}}
```

#### checkmark
Displays a checkmark or X symbol.

| Value | Output |
|-------|--------|
| `true` | ✓ |
| `false` | ✗ |

**Example:**
```
Valid: {{IsValid:checkmark}}
```

#### truefalse
Displays "True" or "False" text (explicit default).

| Value | Output |
|-------|--------|
| `true` | True |
| `false` | False |

**Example:**
```
Debug mode: {{DebugEnabled:truefalse}}
```

#### onoff
Displays "On" or "Off" text.

| Value | Output |
|-------|--------|
| `true` | On |
| `false` | Off |

**Example:**
```
Power: {{PowerStatus:onoff}}
```

#### enabled
Displays "Enabled" or "Disabled" text.

| Value | Output |
|-------|--------|
| `true` | Enabled |
| `false` | Disabled |

**Example:**
```
Feature flag: {{NewFeature:enabled}}
```

#### active
Displays "Active" or "Inactive" text.

| Value | Output |
|-------|--------|
| `true` | Active |
| `false` | Inactive |

**Example:**
```
Account status: {{AccountStatus:active}}
```

### Number and Currency Formatters

#### currency

Formats a number as currency using the configured culture's currency symbol and format.

| Culture | Input | Output |
|---------|-------|--------|
| en-US | 1234.56 | $1,234.56 |
| de-DE | 1234.56 | 1.234,56 € |
| fr-FR | 1234.56 | 1 234,56 € |

**Example:**
```
Total: {{Amount:currency}}
```

**JSON:**
```json
{
  "Amount": 1234.56
}
```

**Output (en-US culture):**
```
Total: $1,234.56
```

#### number:FORMAT

Formats a number using a .NET format string. The format string follows the colon after `number`.

| Specifier | Description | Input | Output |
|-----------|-------------|-------|--------|
| `:number:N2` | Number with 2 decimal places | 1234.5678 | 1,234.57 |
| `:number:N0` | Number with no decimals | 1234.5 | 1,235 |
| `:number:F3` | Fixed-point with 3 decimals | 3.14159 | 3.142 |
| `:number:P` | Percentage | 0.1234 | 12.34 % |
| `:number:C` | Currency (same as `:currency`) | 42 | $42.00 |

**Example:**
```
Value: {{Price:number:N2}}
Rate: {{InterestRate:number:F3}}
Progress: {{Completion:number:P}}
```

**JSON:**
```json
{
  "Price": 1234.5678,
  "InterestRate": 3.14159,
  "Completion": 0.85
}
```

**Output (en-US culture):**
```
Value: 1,234.57
Rate: 3.142
Progress: 85.00 %
```

**Notes:**
- Number formatters only apply to numeric values (int, long, decimal, double, float)
- Non-numeric values with a number format specifier are rendered normally (format is ignored)
- Invalid format strings are handled gracefully — the value falls through to default formatting
- Format specifier names are case-insensitive: `:currency`, `:CURRENCY`, and `:Currency` all work

## Using Format Specifiers

### Basic Usage

**Template:**
```
Name: {{Name}}
Active: {{IsActive:checkbox}}
Verified: {{IsVerified:yesno}}
Valid: {{IsValid:checkmark}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Name"] = "John Doe",
    ["IsActive"] = true,
    ["IsVerified"] = false,
    ["IsValid"] = true
};
```

**JSON Data:**
```json
{
  "Name": "John Doe",
  "IsActive": true,
  "IsVerified": false,
  "IsValid": true
}
```

**Output:**
```
Name: John Doe
Active: ☑
Verified: No
Valid: ✓
```

### With Nested Properties

Format specifiers work seamlessly with nested object properties.

**Template:**
```
User: {{User.Name}}
Status: {{User.IsActive:checkbox}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["User"] = new
    {
        Name = "Jane Smith",
        IsActive = true
    }
};
```

**JSON Data:**
```json
{
  "User": {
    "Name": "Jane Smith",
    "IsActive": true
  }
}
```

**Output:**
```
User: Jane Smith
Status: ☑
```

### With Array Indexing

Format specifiers work with array elements.

**Template:**
```
First item: {{Items[0].Name}}
Active: {{Items[0].IsActive:yesno}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Items"] = new[]
    {
        new { Name = "Item 1", IsActive = true },
        new { Name = "Item 2", IsActive = false }
    }
};
```

**JSON Data:**
```json
{
  "Items": [
    { "Name": "Item 1", "IsActive": true },
    { "Name": "Item 2", "IsActive": false }
  ]
}
```

**Output:**
```
First item: Item 1
Active: Yes
```

### In Loops

Format specifiers are particularly useful in loops to display status indicators.

**Template:**
```
{{#foreach Tasks}}
- {{Name}}: {{IsCompleted:checkbox}}
{{/foreach}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Tasks"] = new[]
    {
        new { Name = "Design mockups", IsCompleted = true },
        new { Name = "Implement feature", IsCompleted = false },
        new { Name = "Write tests", IsCompleted = true }
    }
};
```

**JSON Data:**
```json
{
  "Tasks": [
    { "Name": "Design mockups", "IsCompleted": true },
    { "Name": "Implement feature", "IsCompleted": false },
    { "Name": "Write tests", "IsCompleted": true }
  ]
}
```

**Output:**
```
- Design mockups: ☑
- Implement feature: ☐
- Write tests: ☑
```

### In Conditionals

Use format specifiers within conditional blocks.

**Template:**
```
{{#if ShowStatus}}
Account Status: {{IsActive:active}}
{{/if}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["ShowStatus"] = true,
    ["IsActive"] = true
};
```

**JSON Data:**
```json
{
  "ShowStatus": true,
  "IsActive": true
}
```

**Output:**
```
Account Status: Active
```

## Localization Support

Format specifiers automatically adapt to the culture you specify in `PlaceholderReplacementOptions`.

### German (de-DE)

**C# Code:**
```csharp
var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE"),
    BooleanFormatterRegistry = new BooleanFormatterRegistry(new CultureInfo("de-DE"))
};
var processor = new DocumentTemplateProcessor(options);
```

**Template:**
```
Bestätigt: {{IsConfirmed:yesno}}
```

**Data (C# or JSON):**
```json
{
  "IsConfirmed": true
}
```

**Output:**
```
Bestätigt: Ja
```

### French (fr-FR)

**C# Code:**
```csharp
var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("fr-FR"),
    BooleanFormatterRegistry = new BooleanFormatterRegistry(new CultureInfo("fr-FR"))
};
var processor = new DocumentTemplateProcessor(options);
```

**Template:**
```
Confirmé: {{IsConfirmed:yesno}}
```

**Output:**
```
Confirmé: Oui
```

### Spanish (es-ES)

**C# Code:**
```csharp
var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("es-ES"),
    BooleanFormatterRegistry = new BooleanFormatterRegistry(new CultureInfo("es-ES"))
};
var processor = new DocumentTemplateProcessor(options);
```

**Template:**
```
Confirmado: {{IsConfirmed:yesno}}
```

**Output:**
```
Confirmado: Sí
```

### Supported Languages

The `yesno` formatter currently supports:
- English (en): Yes / No
- German (de): Ja / Nein
- French (fr): Oui / Non
- Spanish (es): Sí / No
- Italian (it): Sì / No
- Portuguese (pt): Sim / Não

Symbol-based formatters (checkbox, checkmark) are culture-independent.

## Custom Formatters

You can create custom boolean formatters for your specific needs.

### Creating a Custom Formatter

**C# Code:**
```csharp
// Create a registry
var registry = new BooleanFormatterRegistry();

// Register a custom formatter
registry.Register("thumbs", new BooleanFormatter("👍", "👎"));

// Use in options
var options = new PlaceholderReplacementOptions
{
    BooleanFormatterRegistry = registry
};
var processor = new DocumentTemplateProcessor(options);
```

**Template:**
```
User feedback: {{IsPositive:thumbs}}
```

**Data:**
```json
{
  "IsPositive": true
}
```

**Output:**
```
User feedback: 👍
```

### Multiple Custom Formatters

**C# Code:**
```csharp
var registry = new BooleanFormatterRegistry();

// Register multiple custom formatters
registry.Register("thumbs", new BooleanFormatter("👍", "👎"));
registry.Register("traffic", new BooleanFormatter("🟢", "🔴"));
registry.Register("stars", new BooleanFormatter("⭐", "☆"));

var options = new PlaceholderReplacementOptions
{
    BooleanFormatterRegistry = registry
};
var processor = new DocumentTemplateProcessor(options);
```

**Template:**
```
Feedback: {{IsPositive:thumbs}}
Status: {{IsOperational:traffic}}
Featured: {{IsFeatured:stars}}
```

**Data:**
```json
{
  "IsPositive": true,
  "IsOperational": false,
  "IsFeatured": true
}
```

**Output:**
```
Feedback: 👍
Status: 🔴
Featured: ⭐
```

## Advanced Usage

### Combining with Expressions

Format specifiers can be combined with boolean expressions for powerful conditional formatting.

See the [Boolean Expressions Guide](boolean-expressions.md) for details.

**Quick Example:**

**Template:**
```
Eligible: {{(Age >= 18 and HasLicense):yesno}}
```

**Data:**
```json
{
  "Age": 20,
  "HasLicense": true
}
```

**Output:**
```
Eligible: Yes
```

### Non-Boolean Values

Format specifiers only apply to boolean values. Non-boolean values are rendered normally.

**Template:**
```
Name: {{Name:checkbox}}
Count: {{Count:yesno}}
```

**Data:**
```json
{
  "Name": "Test",
  "Count": 42
}
```

**Output:**
```
Name: Test
Count: 42
```

### Case Insensitivity

Format specifier names are case-insensitive.

All of these are equivalent:
- `{{IsActive:checkbox}}`
- `{{IsActive:CHECKBOX}}`
- `{{IsActive:CheckBox}}`
- `{{IsActive:ChEcKbOx}}`

### Unknown Formatters

If you specify a formatter that doesn't exist, the value defaults to standard boolean formatting.

**Template:**
```
Status: {{IsActive:unknownformat}}
```

**Data:**
```json
{
  "IsActive": true
}
```

**Output:**
```
Status: True
```

## Best Practices

### 1. Choose Appropriate Formatters

Match the formatter to your document's purpose:
- **checkbox** - Task lists, checklists, forms
- **yesno** - Questions, approvals, confirmations
- **checkmark** - Validation results, requirements met
- **onoff** - Settings, switches, toggles
- **enabled** - Feature flags, capabilities
- **active** - Account status, subscriptions

### 2. Be Consistent

Use the same formatter for similar concepts throughout your document.

**Good:**
```
Task 1: {{Task1.IsCompleted:checkbox}}
Task 2: {{Task2.IsCompleted:checkbox}}
Task 3: {{Task3.IsCompleted:checkbox}}
```

**Avoid:**
```
Task 1: {{Task1.IsCompleted:checkbox}}
Task 2: {{Task2.IsCompleted:yesno}}
Task 3: {{Task3.IsCompleted:checkmark}}
```

### 3. Consider Your Audience

- **Business users** - Prefer text-based formats (yesno, enabled, active)
- **Technical users** - Symbols work well (checkbox, checkmark)
- **International** - Set up proper localization

### 4. Use Symbols Wisely

Symbol-based formatters (checkbox, checkmark) render well in most contexts, but verify they display correctly in your target output format (Word, PDF, etc.).

### 5. Document Custom Formatters

If you create custom formatters, document their meaning for template authors:

```csharp
// thumbs: 👍 for positive feedback, 👎 for negative
registry.Register("thumbs", new BooleanFormatter("👍", "👎"));

// priority: 🔴 for high priority, 🟢 for normal
registry.Register("priority", new BooleanFormatter("🔴", "🟢"));
```

## Real-World Examples

### Project Status Report

**Template:**
```
Project Status Report
=====================

Tasks:
{{#foreach Tasks}}
- {{Name}}: {{IsCompleted:checkbox}}
{{/foreach}}

Milestones:
{{#foreach Milestones}}
- {{Name}}: {{IsReached:checkmark}}
{{/foreach}}

Budget Approved: {{BudgetApproved:yesno}}
Team Active: {{TeamActive:active}}
```

**Data:**
```json
{
  "Tasks": [
    { "Name": "Requirements gathering", "IsCompleted": true },
    { "Name": "Design phase", "IsCompleted": true },
    { "Name": "Development", "IsCompleted": false },
    { "Name": "Testing", "IsCompleted": false }
  ],
  "Milestones": [
    { "Name": "Project kickoff", "IsReached": true },
    { "Name": "Alpha release", "IsReached": false },
    { "Name": "Beta release", "IsReached": false }
  ],
  "BudgetApproved": true,
  "TeamActive": true
}
```

### Employee Checklist

**Template:**
```
Employee Onboarding Checklist
==============================

Employee: {{Employee.Name}}
Department: {{Employee.Department}}

Required Documents:
- ID Verified: {{Documents.IDVerified:checkbox}}
- Background Check: {{Documents.BackgroundCheck:checkbox}}
- Signed Contract: {{Documents.ContractSigned:checkbox}}

System Access:
- Email Account: {{Access.Email:enabled}}
- VPN Access: {{Access.VPN:enabled}}
- Building Access: {{Access.Building:enabled}}

Training Complete: {{Training.Complete:yesno}}
```

**Data:**
```json
{
  "Employee": {
    "Name": "Sarah Johnson",
    "Department": "Engineering"
  },
  "Documents": {
    "IDVerified": true,
    "BackgroundCheck": true,
    "ContractSigned": true
  },
  "Access": {
    "Email": true,
    "VPN": true,
    "Building": false
  },
  "Training": {
    "Complete": false
  }
}
```

### Service Health Dashboard

**Template:**
```
Service Health Dashboard
========================

Core Services:
{{#foreach Services}}
- {{Name}}: {{IsOperational:traffic}}  {{IsOperational:active}}
{{/foreach}}

Automated Backups: {{BackupsEnabled:onoff}}
Monitoring: {{MonitoringActive:enabled}}
```

**C# Code (with custom formatter):**
```csharp
var registry = new BooleanFormatterRegistry();
registry.Register("traffic", new BooleanFormatter("🟢", "🔴"));

var options = new PlaceholderReplacementOptions
{
    BooleanFormatterRegistry = registry
};
var processor = new DocumentTemplateProcessor(options);
```

**Data:**
```json
{
  "Services": [
    { "Name": "API Gateway", "IsOperational": true },
    { "Name": "Database", "IsOperational": true },
    { "Name": "Cache Server", "IsOperational": false },
    { "Name": "Message Queue", "IsOperational": true }
  ],
  "BackupsEnabled": true,
  "MonitoringActive": true
}
```

## Summary

Format specifiers provide a powerful way to control value presentation in your documents:

- ✅ 7 built-in boolean formatters (checkbox, yesno, checkmark, truefalse, onoff, enabled, active)
- ✅ Currency formatting with locale support (`:currency`)
- ✅ Flexible number formatting with .NET format strings (`:number:N2`, `:number:F3`, `:number:P`)
- ✅ Automatic localization support
- ✅ Custom formatter registration
- ✅ Works with nested properties, arrays, loops, and conditionals
- ✅ Combines with boolean expressions
- ✅ Case-insensitive format names
- ✅ Same data works with C# Dictionaries or JSON

For more advanced usage, see:
- [Boolean Expressions Guide](boolean-expressions.md) - Combine expressions with formatters
- [API Reference](../../TriasDev.Templify/README.md) - Complete API documentation
- [FAQ](../FAQ.md) - Common questions and answers
