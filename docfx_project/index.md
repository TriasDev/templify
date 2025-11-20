# Welcome to TriasDev.Templify

**High-performance Word document templating engine for .NET**

Templify allows you to replace placeholders, evaluate conditionals, and process loops in .docx files without Microsoft Word. Built on the OpenXML SDK with 100% test coverage.

## Features

- **Placeholder Replacement**: Replace `{{VariableName}}` with values from your data
- **Nested Properties**: Access nested data with `{{Customer.Address.City}}`
- **Conditionals**: Use `{{#if Condition}}...{{/if}}` blocks with full boolean expressions
- **Loops**: Process collections with `{{#foreach Items}}...{{/foreach}}`
- **Markdown Support**: Format text with **bold**, *italic*, and ~~strikethrough~~
- **Formatting Preservation**: Maintains fonts, colors, styles, and list formatting
- **Table Operations**: Process rows, cells, and nested structures
- **100% Test Coverage**: 109+ tests ensure reliability

## Quick Start

Install via NuGet:

```bash
dotnet add package TriasDev.Templify
```

Basic usage:

```csharp
using TriasDev.Templify;

// Load your template
using var template = File.OpenRead("template.docx");
using var output = File.Create("output.docx");

// Prepare your data
var data = new Dictionary<string, object>
{
    ["CompanyName"] = "Acme Corp",
    ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"),
    ["Items"] = new[]
    {
        new { Name = "Product A", Price = 99.99 },
        new { Name = "Product B", Price = 149.99 }
    }
};

// Process the template
var processor = new DocumentTemplateProcessor();
var result = processor.ProcessTemplate(template, output, data);

if (result.IsSuccess)
{
    Console.WriteLine("Template processed successfully!");
}
```

## Documentation

- [API Reference](api/index.md) - Complete API documentation
- [Quick Start Guide](articles/quick-start.md) - Get up and running in 5 minutes
- [Tutorials](articles/tutorials/toc.yml) - Step-by-step tutorials
- [Guides](articles/guides/toc.yml) - Feature guides
- [FAQ](articles/FAQ.md) - Frequently asked questions

## Links

- [GitHub Repository](https://github.com/TriasDev/templify)
- [NuGet Package](https://www.nuget.org/packages/TriasDev.Templify)
- [Report Issues](https://github.com/TriasDev/templify/issues)
- [Contributing Guide](https://github.com/TriasDev/templify/blob/main/CONTRIBUTING.md)

## License

Templify is licensed under the [MIT License](https://github.com/TriasDev/templify/blob/main/LICENSE).
