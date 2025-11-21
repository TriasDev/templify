# Templify Examples

This folder contains downloadable example templates and sample data files that you can use to learn Templify.

## Available Examples

Each example folder contains:

- **template.docx** - The Word template with Templify placeholders
- **data.json** - Sample JSON data to fill the template
- **output.docx** - Pre-generated output showing what the result looks like

## Examples (Coming Soon)

### hello-world/
A simple introduction demonstrating basic placeholder replacement.

**Features:**
- Simple placeholders
- Text replacement
- Beginner-friendly

### invoice/
A professional invoice template with line items and calculations.

**Features:**
- Nested properties (`Customer.Name`, `Customer.Address`)
- Table row loops for line items
- Number formatting
- Multiple sections

### conditionals/
Demonstrates conditional sections that show/hide based on data.

**Features:**
- If/else logic
- Boolean flags
- Status-based content
- Multiple conditionals

### nested-loops/
Shows how to work with hierarchical data.

**Features:**
- Nested loops (departments → employees)
- Multi-level data structures
- Parent context access

## How to Use These Examples

### 1. Download Files

Download both the template and data files from the example folder you want to try.

### 2. Process the Template

**Option A: Using Templify GUI**

1. Open the Templify GUI application
2. Click "Select Template" and choose the `template.docx` file
3. Click "Select Data" and choose the `data.json` file
4. Click "Process Template"
5. Save the output

**Option B: Using Templify CLI**

```bash
templify process template.docx --data data.json --output my-output.docx
```

**Option C: Using Code (C#)**

```csharp
using TriasDev.Templify;

var data = JsonDataParser.ParseJsonFile("data.json");
var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("template.docx");
using var outputStream = File.Create("my-output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### 3. Compare with Pre-Generated Output

Open the included `output.docx` file to see what the expected result looks like.

### 4. Experiment!

- **Modify the JSON data** - Change values, add items to arrays, etc.
- **Edit the template** - Add new placeholders, change formatting
- **Create variations** - Try different conditional values
- **Learn by doing** - Break things and fix them!

## Creating Your Own Templates

After trying these examples:

1. Start with a simple example (hello-world)
2. Modify it to match your use case
3. Gradually add complexity (conditionals, loops)
4. Refer to the [Template Author Documentation](../docs/for-template-authors/getting-started.md)

## Tips

- **Validate JSON** - Use [jsonlint.com](https://jsonlint.com) to check JSON syntax
- **Start simple** - Begin with basic placeholders before adding loops/conditionals
- **Test incrementally** - Make small changes and test frequently
- **Read the guides** - Check [docs/for-template-authors/](../docs/for-template-authors/) for detailed explanations

## Need Help?

- **[Template Author Documentation](../docs/for-template-authors/getting-started.md)** - Complete guide
- **[Examples Gallery](../docs/for-template-authors/examples-gallery.md)** - Visual examples
- **[FAQ](../docs/FAQ.md)** - Common questions
- **[GitHub Issues](https://github.com/triasdev/templify/issues)** - Report problems

---

*Examples are automatically generated using the Templify DocumentGenerator tool to ensure accuracy.*
