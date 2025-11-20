# Quick Start Guide

Get up and running with Templify in 5 minutes and generate your first Word document.

## Prerequisites

- .NET 6.0 SDK or later ([Download](https://dotnet.microsoft.com/download))
- A code editor (Visual Studio 2022, VS Code, or Rider)
- Basic C# knowledge

## Installation

### Via NuGet Package Manager

```bash
dotnet add package TriasDev.Templify
```

### Via Package Manager Console (Visual Studio)

```powershell
Install-Package TriasDev.Templify
```

### Via .csproj File

```xml
<ItemGroup>
  <PackageReference Include="TriasDev.Templify" Version="1.0.0" />
</ItemGroup>
```

## Your First Template in 3 Steps

### Step 1: Create a Word Template

Open Microsoft Word and create a new document with this content:

```
Hello {{Name}}!

This is your invoice #{{InvoiceNumber}} dated {{Date}}.

Total Amount: {{Amount}} EUR
```

Save it as `template.docx` in your project directory.

**What are placeholders?**
- Placeholders are enclosed in double curly braces: `{{VariableName}}`
- They will be replaced with actual data when you process the template
- Use simple variable names (letters, numbers, underscore)

### Step 2: Write the Code

Create a new C# file and add this code:

```csharp
using TriasDev.Templify;

class Program
{
    static void Main()
    {
        // Create the template processor
        var processor = new DocumentTemplateProcessor();

        // Define your data
        var data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["InvoiceNumber"] = "INV-2025-001",
            ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["Amount"] = 1250.50m
        };

        // Process the template
        using var templateStream = File.OpenRead("template.docx");
        using var outputStream = File.Create("output.docx");

        var result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Check the result
        if (result.IsSuccessful)
        {
            Console.WriteLine("✓ Document generated successfully!");
            Console.WriteLine($"  Placeholders replaced: {result.PlaceholdersReplaced}");
        }
        else
        {
            Console.WriteLine("✗ Errors occurred:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }
    }
}
```

### Step 3: Run and View Output

```bash
dotnet run
```

Open `output.docx` and you'll see:

```
Hello John Doe!

This is your invoice #INV-2025-001 dated 2025-01-15.

Total Amount: 1250.50 EUR
```

🎉 **Congratulations!** You've generated your first document with Templify!

---

## What Just Happened?

1. **Template Creation**: You created a Word document with `{{placeholders}}`
2. **Data Preparation**: You provided data as a `Dictionary<string, object>`
3. **Processing**: Templify replaced all placeholders with your data
4. **Output**: You got a new Word document with actual values

---

## Common Scenarios

### Working with Nested Data

```csharp
var data = new Dictionary<string, object>
{
    ["Company"] = new Dictionary<string, object>
    {
        ["Name"] = "Acme Corp",
        ["Address"] = new Dictionary<string, object>
        {
            ["Street"] = "123 Main St",
            ["City"] = "Springfield",
            ["Zip"] = "12345"
        }
    }
};
```

**Template:**
```
Company: {{Company.Name}}
Address: {{Company.Address.Street}}, {{Company.Address.City}} {{Company.Address.Zip}}
```

### Working with Objects (POCOs)

```csharp
public class Company
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string Zip { get; set; }
}

var data = new Dictionary<string, object>
{
    ["Company"] = new Company
    {
        Name = "Acme Corp",
        Address = new Address
        {
            Street = "123 Main St",
            City = "Springfield",
            Zip = "12345"
        }
    }
};
```

The template syntax is the same!

### Error Handling

```csharp
var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (!result.IsSuccessful)
{
    Console.WriteLine("Errors:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}

if (result.MissingVariables.Any())
{
    Console.WriteLine("Warning - Missing variables:");
    foreach (var variable in result.MissingVariables)
    {
        Console.WriteLine($"  - {variable}");
    }
}

Console.WriteLine($"Statistics:");
Console.WriteLine($"  Placeholders replaced: {result.PlaceholdersReplaced}");
Console.WriteLine($"  Conditionals evaluated: {result.ConditionalsEvaluated}");
Console.WriteLine($"  Loops processed: {result.LoopsProcessed}");
```

---

## Bonus: Format Specifiers & Expressions

Templify includes powerful features for formatting boolean values and evaluating logic:

### Format Booleans

Transform boolean values into checkboxes, Yes/No, and more:

**Template:**
```
Task completed: {{IsCompleted:checkbox}}
Approved: {{IsApproved:yesno}}
Valid: {{IsValid:checkmark}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["IsCompleted"] = true,
    ["IsApproved"] = false,
    ["IsValid"] = true
};
```

**Output:**
```
Task completed: ☑
Approved: No
Valid: ✓
```

**Available formatters:** `checkbox`, `yesno`, `checkmark`, `truefalse`, `onoff`, `enabled`, `active`

### Boolean Expressions

Evaluate logic directly in placeholders:

**Template:**
```
Eligible: {{(Age >= 18):yesno}}
Access: {{(IsActive and IsVerified):checkbox}}
```

**Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Age"] = 25,
    ["IsActive"] = true,
    ["IsVerified"] = true
};
```

**Output:**
```
Eligible: Yes
Access: ☑
```

### Using JSON Data

Business users can provide data as JSON:

**data.json:**
```json
{
  "Name": "John Doe",
  "IsActive": true,
  "Items": [
    { "Name": "Product A", "Price": 10.00 }
  ]
}
```

**C# Code:**
```csharp
using System.Text.Json;

string jsonText = File.ReadAllText("data.json");
var data = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);

processor.ProcessTemplate(templateStream, outputStream, data);
```

📚 **Learn More:**
- [Format Specifiers Guide](guides/format-specifiers.md) - Complete formatting reference
- [Boolean Expressions Guide](guides/boolean-expressions.md) - Logic evaluation reference

---

## Next Steps

Now that you've generated your first document, explore more advanced features:

### 📚 Tutorials (Step-by-Step Learning)
- [Tutorial 1: Hello World](tutorials/01-hello-world.md) - Expand on basics (30 min)
- [Tutorial 2: Invoice Generator](tutorials/02-invoice-generator.md) - Real-world example (1 hour)
- [Tutorial 3: Conditionals & Loops](tutorials/03-conditionals-and-loops.md) - Dynamic content (1 hour)
- [Tutorial 4: Advanced Features](tutorials/04-advanced-features.md) - Master Templify (1.5 hours)

### 📖 Documentation
- [Complete API Reference](../TriasDev.Templify/README.md) - All features explained
- [Architecture Guide](../TriasDev.Templify/ARCHITECTURE.md) - How Templify works internally
- [Examples Collection](../TriasDev.Templify/Examples.md) - 1,900+ lines of code samples
- [Performance Benchmarks](../TriasDev.Templify/PERFORMANCE.md) - Speed and optimization

### 🎯 Common Use Cases
- **Invoice Generation** - With line items and calculations
- **Report Generation** - Dynamic sections and data tables
- **Contract Templates** - Conditional clauses
- **Certificate Generation** - Personalized documents
- **Letter Templates** - Mail merge scenarios

### ❓ Get Help
- [FAQ](FAQ.md) - Common questions and answers
- [Troubleshooting](../TriasDev.Templify/README.md#troubleshooting) - Solve common issues
- [GitHub Issues](https://github.com/TriasDev/templify/issues) - Report bugs or ask questions

---

## Tips for Success

### ✅ DO
- Use descriptive variable names: `{{CustomerName}}` not `{{cn}}`
- Test with small templates first
- Check `result.MissingVariables` to catch typos
- Use nested objects for complex data structures
- Keep templates simple and readable

### ❌ DON'T
- Use special characters in placeholder names (stick to letters, numbers, underscore)
- Nest placeholders: `{{{{nested}}}}` won't work
- Forget to dispose streams (use `using` statements)
- Ignore error handling in production code
- Create overly complex templates (split into multiple if needed)

---

## Quick Reference

### Basic Syntax

| Template Syntax | Description | Example Data |
|-----------------|-------------|--------------|
| `{{Name}}` | Simple placeholder | `["Name"] = "John"` |
| `{{User.Email}}` | Nested property | Object with `Email` property |
| `{{Items[0]}}` | Array index | List or array |
| `{{IsActive:checkbox}}` | Format specifier | `["IsActive"] = true` → ☑ |
| `{{(Age >= 18):yesno}}` | Boolean expression | `["Age"] = 25` → Yes |
| `{{#if Active}}...{{/if}}` | Conditional | `["Active"] = true` |
| `{{#foreach Items}}...{{/foreach}}` | Loop | List or array |

### Common Data Types

```csharp
// Strings
["Name"] = "John Doe"

// Numbers
["Amount"] = 1250.50m
["Count"] = 42

// Booleans
["IsActive"] = true

// Dates
["Date"] = DateTime.Now

// Objects
["User"] = new { Name = "John", Email = "john@example.com" }

// Collections
["Items"] = new List<string> { "Item 1", "Item 2" }
```

---

## What's Special About Templify?

✨ **Simple** - Just `{{placeholders}}` in Word, no complex setup
⚡ **Fast** - Process thousands of documents per second
🎨 **Format-Preserving** - Bold, colors, fonts maintained automatically
🔒 **Type-Safe** - Strong typing in C#, flexible at runtime
📦 **Zero Dependencies** - Only needs OpenXML SDK
✅ **Battle-Tested** - Used in production in enterprise platforms
🧪 **100% Tested** - Complete test coverage, reliable
📖 **Well-Documented** - Comprehensive guides and examples

---

## Need More Help?

- 📖 [Full Documentation](../TriasDev.Templify/README.md)
- 💬 [GitHub Discussions](https://github.com/TriasDev/templify/discussions)
- 🐛 [Report Issues](https://github.com/TriasDev/templify/issues)
- ⭐ [Star on GitHub](https://github.com/TriasDev/templify)

**Happy templating!** 🚀
