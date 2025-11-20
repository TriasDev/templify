# Templify Documentation

Welcome to **Templify** - a .NET library for replacing placeholders in Word documents without requiring Microsoft Word.

## What is Templify?

Templify provides a simple, intuitive way to generate Word documents from templates with placeholders (`{{variableName}}`), conditionals, and loops. Perfect for generating invoices, reports, contracts, and any other document-based automation.

## Key Features

✨ **Simple Placeholder Replacement** - `{{VariableName}}` syntax
🔁 **Loops** - Repeat sections with `{{#foreach}}...{{/foreach}}`
⚡ **Conditionals** - Dynamic content with `{{#if}}...{{else}}...{{/if}}`
📊 **Table Support** - Loop through table rows with data
🎨 **Formatting Preservation** - Maintains Word document styling
🚀 **No Microsoft Word Required** - Uses Open XML SDK

## Quick Example

```csharp
using TriasDev.Templify;

var data = new Dictionary<string, object>
{
    ["CustomerName"] = "John Doe",
    ["InvoiceDate"] = DateTime.Now.ToString("yyyy-MM-dd"),
    ["Items"] = new List<Dictionary<string, object>>
    {
        new() { ["Product"] = "Service A", ["Price"] = "$100" },
        new() { ["Product"] = "Service B", ["Price"] = "$200" }
    }
};

var processor = new DocumentTemplateProcessor();
using var templateStream = File.OpenRead("invoice-template.docx");
using var outputStream = File.Create("invoice-output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

## Get Started

### 📚 [Quick Start Guide](quick-start.md)
Install Templify and create your first document in 5 minutes

### 🎓 [Tutorials](tutorials/)
Step-by-step guides from basics to advanced features

### 📖 [Feature Guides](guides/)
In-depth guides for specific features and use cases

### ❓ [FAQ](FAQ.md)
Common questions and troubleshooting tips

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package TriasDev.Templify
```

Or via Package Manager Console:

```powershell
Install-Package TriasDev.Templify
```

## Use Cases

- **Invoices & Receipts** - Generate customer invoices with line items
- **Reports** - Create formatted reports from database data
- **Contracts** - Generate contracts with dynamic clauses
- **Letters** - Mail merge functionality for letters
- **Certificates** - Batch generate certificates with participant data

## Target Framework

- **.NET 6.0 or later** - Supports .NET 6.0, 8.0, and 9.0

## License

Templify is open source and licensed under the [MIT License](https://github.com/triasdev/templify/blob/main/LICENSE).

## Support

- 📖 [Documentation](quick-start.md)
- 🐛 [Report Issues](https://github.com/triasdev/templify/issues)
- 💬 [Discussions](https://github.com/triasdev/templify/discussions)
