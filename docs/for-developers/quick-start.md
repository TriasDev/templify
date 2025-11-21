# Developer Documentation - Coming Soon!

We're currently reorganizing our documentation to better serve both template authors and developers.

## What's Coming

The developer documentation section will include:

- **Installation Guide** - NuGet package installation and setup
- **Quick Start Guide** - Your first Templify integration in C#
- **API Reference** - Complete API documentation with examples
- **Code Examples** - Real-world integration patterns
- **Architecture Overview** - Understanding the visitor pattern and internals
- **Performance Guide** - Optimization tips and benchmarks

## In the Meantime

You can find comprehensive developer documentation in:

- **[Main Library README](../../TriasDev.Templify/README.md)** - Complete API reference with C# examples
- **[Examples.md](../../TriasDev.Templify/Examples.md)** - Extensive code samples
- **[Architecture.md](../../TriasDev.Templify/ARCHITECTURE.md)** - Detailed architecture documentation

## Quick Installation

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
