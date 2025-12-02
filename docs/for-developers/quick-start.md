# Quick Start for Developers

Get started with Templify in your .NET application.

## Installation

```bash
dotnet add package TriasDev.Templify
```

## Basic Usage

```csharp
using TriasDev.Templify;

var data = new Dictionary<string, object>
{
    ["CustomerName"] = "John Doe",
    ["OrderDate"] = DateTime.Now.ToString("yyyy-MM-dd")
};

var processor = new DocumentTemplateProcessor();
using var templateStream = File.OpenRead("template.docx");
using var outputStream = File.Create("output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.Success)
{
    Console.WriteLine("Template processed successfully!");
}
```

## Need Help?

- 🐛 [Report Issues](https://github.com/triasdev/templify/issues)
- 💬 [Discussions](https://github.com/triasdev/templify/discussions)
- 📖 [Current Documentation](../../TriasDev.Templify/README.md)
