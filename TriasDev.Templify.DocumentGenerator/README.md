# Templify Documentation Example Generator

A tool for automatically generating example Word documents and converting them to images for documentation.

## What It Does

1. **Generates Example Templates**: Creates .docx template files with Templify syntax
2. **Processes Templates**: Uses Templify to generate output documents
3. **Converts to Images**: Uses Stirling-PDF to create PNG screenshots for documentation

## Quick Start

### Prerequisites

- .NET 9 SDK
- Access to a Stirling-PDF instance (for image generation)

### Setup

1. Copy the environment file:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` and configure your Stirling-PDF instance:
   ```env
   STIRLING_PDF_URL=https://your-stirling-instance.com
   STIRLING_PDF_API_KEY=your-api-key-here
   ```

### Usage

**Generate all examples (documents + images):**
```bash
dotnet run --project TriasDev.Templify.DocumentGenerator
```

**Generate specific example:**
```bash
dotnet run --project TriasDev.Templify.DocumentGenerator -- hello-world
dotnet run --project TriasDev.Templify.DocumentGenerator -- invoice
dotnet run --project TriasDev.Templify.DocumentGenerator -- conditionals
```

**Skip image generation:**
```bash
dotnet run --project TriasDev.Templify.DocumentGenerator -- --skip-images
```

## Output

The tool generates files in the following locations:

```
examples/
├── templates/          # Template .docx files (with {{placeholders}})
└── outputs/            # Processed .docx files (with actual data)

docfx_project/images/examples/
├── templates/          # PNG screenshots of templates
└── outputs/            # PNG screenshots of outputs
```

## Available Examples

### 1. Hello World (`hello-world`)
- **Purpose**: Demonstrates basic placeholder replacement
- **Features**:
  - Simple variables (FirstName, LastName, Date)
  - Numbers and booleans
  - Nested data (Company.Name, Company.City)

### 2. Invoice (`invoice`)
- **Purpose**: Demonstrates complex documents with loops
- **Features**:
  - Table loops ({{#foreach Items}})
  - Multiple sections (header, customer, line items, totals)
  - Real-world invoice structure

### 3. Conditionals (`conditionals`)
- **Purpose**: Demonstrates conditional logic
- **Features**:
  - If/else blocks
  - Boolean expressions (=, !=, and, or)
  - Nested conditionals
  - Multiple condition types

## Architecture

### Core Components

**`BaseExampleGenerator`**
- Abstract base class for generators
- Provides helper methods for document creation
- Handles Templify processing

**`IExampleGenerator`**
- Interface defining generator contract
- Methods: `GenerateTemplate()`, `GetSampleData()`, `ProcessTemplate()`

**`StirlingPdfConverter`**
- Integrates with Stirling-PDF API
- Converts DOCX → PDF → PNG
- Handles multi-page documents (extracts first page)

**`Program.cs`**
- CLI entry point
- Orchestrates generation and conversion
- Loads configuration from .env

### Flow

```
1. Load .env configuration
2. Create generators (HelloWorld, Invoice, Conditionals)
3. For each generator:
   ├─▶ Generate template.docx (using OpenXML)
   ├─▶ Process with Templify → output.docx
   └─▶ Convert to PNG (via Stirling-PDF)
4. Display summary
```

## Adding New Examples

1. Create a new generator class:
   ```csharp
   public class MyExampleGenerator : BaseExampleGenerator
   {
       public override string Name => "my-example";
       public override string Description => "What this example demonstrates";

       public override string GenerateTemplate(string outputDirectory)
       {
           var path = Path.Combine(outputDirectory, $"{Name}-template.docx");
           using (var doc = CreateDocument(path))
           {
               var body = doc.MainDocumentPart!.Document.Body!;

               AddParagraph(body, "Hello {{Name}}!");
               // Add more content...

               doc.Save();
           }
           return path;
       }

       public override Dictionary<string, object> GetSampleData()
       {
           return new Dictionary<string, object>
           {
               ["Name"] = "World"
           };
       }
   }
   ```

2. Register in `Program.cs`:
   ```csharp
   var generators = new List<IExampleGenerator>
   {
       new HelloWorldGenerator(),
       new InvoiceGenerator(),
       new ConditionalGenerator(),
       new MyExampleGenerator(),  // Add here
   };
   ```

3. Run the generator:
   ```bash
   dotnet run --project TriasDev.Templify.DocumentGenerator -- my-example
   ```

## Helper Methods

The `BaseExampleGenerator` provides useful methods:

### Document Creation
```csharp
var doc = CreateDocument(path);  // Creates a new Word document
```

### Adding Content
```csharp
AddParagraph(body, "Hello World");                    // Plain text
AddParagraph(body, "Bold Text", isBold: true);        // Bold text
AddPlaceholder(body, "VariableName");                 // {{VariableName}}
AddEmptyParagraph(body);                              // Spacing
```

### Tables
```csharp
var table = CreateTable(columns: 3);
AddTableHeaderRow(table, "Col1", "Col2", "Col3");
AddTableRow(table, "Data1", "Data2", "Data3");
```

### Table Loops
```csharp
var table = CreateTable(3);
AddTableHeaderRow(table, "Name", "Quantity", "Price");

// Loop markers
AddTableRow(table, "{{#foreach Items}}", "", "");
AddTableRow(table, "{{Name}}", "{{Quantity}}", "{{Price}}");
AddTableRow(table, "{{/foreach}}", "", "");
```

## Using Images in Documentation

After generating images, reference them in your markdown files:

```markdown
## Example: Hello World

**Template:**
![Hello World Template](../../images/examples/templates/hello-world-template.png)

**Output:**
![Hello World Output](../../images/examples/outputs/hello-world-output.png)

The template shows how to use placeholders like `{{FirstName}}` and `{{Company.Name}}`.
```

## Configuration

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `STIRLING_PDF_URL` | Yes | URL of your Stirling-PDF instance |
| `STIRLING_PDF_API_KEY` | No | API key if authentication is required |

### Stirling-PDF Setup

The tool requires a running Stirling-PDF instance. Options:

1. **Docker (Recommended)**:
   ```bash
   docker run -d -p 8080:8080 frooodle/s-pdf:latest
   ```

2. **Hosted Instance**: Use a remote Stirling-PDF service

3. **Local Build**: Build and run Stirling-PDF locally

## Troubleshooting

### "Cannot connect to Stirling-PDF"

**Solution**:
- Verify Stirling-PDF is running
- Check URL in `.env` file
- Test connectivity: `curl https://your-stirling-instance.com`
- Verify API key is correct

### "STIRLING_PDF_URL not configured"

**Solution**:
- Create `.env` file from `.env.example`
- Set `STIRLING_PDF_URL` variable
- Restart the tool

### Low Quality Images

**Solution**:
- Increase DPI in `StirlingPdfConverter.cs`
- Change `"dpi", "200"` to `"dpi", "300"`

### Multi-Page Documents

Currently, only the first page is captured. To capture all pages:
- Modify `ExtractFirstPngFromZip()` to extract all images
- Save them as `{name}-page-1.png`, `{name}-page-2.png`, etc.

## Benefits

✅ **Automated**: Regenerate all examples with one command
✅ **Consistent**: Same quality and format across all examples
✅ **Up-to-date**: Examples always match current Templify version
✅ **Visual**: Documentation includes actual screenshots
✅ **Reproducible**: Images generated from code, not manual screenshots

## Performance

- Document generation: < 1s per example
- Image conversion: ~2-5s per document (depends on Stirling-PDF)
- Total time for all examples: ~20-30s

## Related Documentation

- [Templify Main README](../README.md)
- [Templify Examples](../TriasDev.Templify/Examples.md)
- [Scripts README](../scripts/README.md)
- [Contributing Guidelines](../CONTRIBUTING.md)
