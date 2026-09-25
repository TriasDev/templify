# Tutorial 2: Building an Invoice Generator

**Duration**: 1 hour
**Difficulty**: Intermediate
**Prerequisites**: [Tutorial 1: Hello World](01-hello-world.md) completed

---

## What You'll Learn

- Working with collections (lists, arrays)
- Using loops to repeat content
- Loop special variables (`@index`, `@number`, `@first`, `@last`, `@count`)
- Creating dynamic tables with row loops
- Formatting numbers and dates in the template
- Building a real-world invoice template

---

## The Goal

Build a complete invoice generator that produces professional invoices like this:

```
INVOICE #INV-2025-001                          Date: 15.01.2025

Bill To:
Acme Corporation
123 Main Street
Springfield, IL 62701

Line Items:
┌────┬───────────────────────────────┬─────┬──────────┬────────────┐
│ #  │ Description                   │ Qty │ Price    │ Total      │
├────┼───────────────────────────────┼─────┼──────────┼────────────┤
│ 1  │ Software Enterprise License   │ 5   │ 499,00 € │ 2.495,00 € │
│ 2  │ Support & Maintenance (Annual)│ 5   │ 99,00 €  │ 495,00 €   │
│ 3  │ Training Package (2 days)     │ 2   │ 250,00 € │ 500,00 €   │
└────┴───────────────────────────────┴─────┴──────────┴────────────┘

                                              Subtotal: 3.490,00 €
                                                   Tax: 663,10 €
                                                 Total: 4.153,10 €

Payment due within 30 days.
```

The numbers use German formatting because the processor is configured with the `de-DE` culture (Step 6).

---

## Step 1: Understanding Loops

Before building the invoice, let's understand how loops work.

### Basic Loop Syntax

**Template** (each marker in its own paragraph):
```
{{#foreach Items}}
- {{.}}
{{/foreach}}
```

**Data**:
```csharp
["Items"] = new List<string> { "Apple", "Banana", "Cherry" }
```

**Output**:
```
- Apple
- Banana
- Cherry
```

For a collection of simple values such as strings, `{{.}}` (or `{{this}}`) is the current item.

The `{{#foreach}}` and `{{/foreach}}` markers must each be in a paragraph of their own: the paragraphs containing
the markers are removed from the output, including any other text in them.

### Loop with Objects

**Template**:
```
{{#foreach Products}}
{{Name}}: {{Price}} EUR
{{/foreach}}
```

**Data**:
```csharp
["Products"] = new List<object>
{
    new { Name = "Laptop", Price = 999.00m },
    new { Name = "Mouse", Price = 29.00m },
    new { Name = "Keyboard", Price = 79.00m }
}
```

**Output** (with an English culture; see [Format Specifiers](../for-template-authors/format-specifiers.md)):
```
Laptop: 999.00 EUR
Mouse: 29.00 EUR
Keyboard: 79.00 EUR
```

Inside the loop, `{{Name}}` and `{{Price}}` are properties of the current item. Names that the item does not have
are looked up in the outer data.

---

## Step 2: Loop Special Variables

Inside loops, you have access to special variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `@index` | Current position (0-based) | 0, 1, 2, ... |
| `@number` | Current position (1-based) | 1, 2, 3, ... |
| `@first` | True for first item | True, False, False |
| `@last` | True for last item | False, False, True |
| `@count` | Total number of items | 3, 3, 3 |

**Template Example**:
```
{{#foreach Items}}
Item {{@number}} of {{@count}}: {{.}}{{#if @last}} (final item){{/if}}
{{/foreach}}
```

**Output**:
```
Item 1 of 3: Apple
Item 2 of 3: Banana
Item 3 of 3: Cherry (final item)
```

These variables only exist inside a loop. Outside a loop, `{{@count}}` is a missing variable; to print the
number of items there, use the collection's `Count` property: `{{Items.Count}}`.

---

## Step 3: Create the Invoice Template

Open Word and create `invoice-template.docx`:

```
INVOICE #{{InvoiceNumber}}                    Date: {{InvoiceDate:date:dd.MM.yyyy}}

Bill To:
{{BillTo.CompanyName}}
{{BillTo.Street}}
{{BillTo.City}}, {{BillTo.State}} {{BillTo.Zip}}

Line Items:
```

Now create a table with this structure. The loop markers go into **their own rows**; Templify repeats the rows
between them for each item and removes the marker rows:

| # | Description | Quantity | Unit Price | Total |
|---|-------------|----------|------------|-------|
| {{#foreach LineItems}} | | | | |
| {{@number}} | {{Description}} | {{Quantity}} | {{UnitPrice:currency}} | {{LineTotal:currency}} |
| {{/foreach}} | | | | |

After the table, add:

```
                                              Subtotal: {{Subtotal:currency}}
                                                   Tax: {{Tax:currency}}
                                                 Total: {{Total:currency}}

{{PaymentTerms}}
```

`:currency` and `:date:...` format numbers and dates with the processor's culture, so the data can stay numeric.

---

## Step 4: Define Data Classes

```csharp
public class Invoice
{
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public Address BillTo { get; set; } = new();
    public List<LineItem> LineItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string PaymentTerms { get; set; } = "";
}

public class Address
{
    public string CompanyName { get; set; } = "";
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class LineItem
{
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}
```

---

## Step 5: Generate Invoice Data

Calculations belong in code; the template only displays the results:

```csharp
public static class InvoiceFactory
{
    private const decimal TaxRate = 0.19m; // 19% VAT

    public static Invoice CreateSampleInvoice()
    {
        var lineItems = new List<LineItem>
        {
            new LineItem { Description = "Software Enterprise License", Quantity = 5, UnitPrice = 499.00m },
            new LineItem { Description = "Support & Maintenance (Annual)", Quantity = 5, UnitPrice = 99.00m },
            new LineItem { Description = "Training Package (2 days)", Quantity = 2, UnitPrice = 250.00m }
        };

        decimal subtotal = lineItems.Sum(item => item.LineTotal);
        decimal tax = Math.Round(subtotal * TaxRate, 2);

        return new Invoice
        {
            InvoiceNumber = "INV-2025-001",
            InvoiceDate = new DateTime(2025, 1, 15),
            BillTo = new Address
            {
                CompanyName = "Acme Corporation",
                Street = "123 Main Street",
                City = "Springfield",
                State = "IL",
                Zip = "62701"
            },
            LineItems = lineItems,
            Subtotal = subtotal,
            Tax = tax,
            Total = subtotal + tax,
            PaymentTerms = "Payment due within 30 days. Thank you for your business!"
        };
    }
}
```

---

## Step 6: Process the Invoice

```csharp
using System.Globalization;
using TriasDev.Templify.Core;

Invoice invoice = InvoiceFactory.CreateSampleInvoice();

var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = invoice.InvoiceNumber,
    ["InvoiceDate"] = invoice.InvoiceDate,
    ["BillTo"] = invoice.BillTo,
    ["LineItems"] = invoice.LineItems,
    ["Subtotal"] = invoice.Subtotal,
    ["Tax"] = invoice.Tax,
    ["Total"] = invoice.Total,
    ["PaymentTerms"] = invoice.PaymentTerms
};

var processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions
{
    Culture = CultureInfo.GetCultureInfo("de-DE")
});

using var templateStream = File.OpenRead("invoice-template.docx");
using var outputStream = File.Create($"invoice-{invoice.InvoiceNumber}.docx");

ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.IsSuccess)
{
    Console.WriteLine($"✓ Invoice {invoice.InvoiceNumber} generated!");
    Console.WriteLine($"  Line items: {invoice.LineItems.Count}");
    Console.WriteLine($"  Total: {invoice.Total:C}");
}
else
{
    Console.WriteLine($"✗ Error: {result.ErrorMessage}");
}
```

Place the classes from Step 4 and Step 5 after these statements in `Program.cs` (or in separate files).

---

## Step 7: Formatting Choices

The template decides how values look; the data stays typed:

| Template | Value | Output (`de-DE`) | Output (`en-US`) |
|----------|-------|------------------|------------------|
| `{{Total:currency}}` | `4153.10m` | `4.153,10 €` | `$4,153.10` |
| `{{Total:number:N2}}` | `4153.10m` | `4.153,10` | `4,153.10` |
| `{{InvoiceDate:date:dd.MM.yyyy}}` | `2025-01-15` | `15.01.2025` | `15.01.2025` |
| `{{InvoiceDate:date:MMMM d, yyyy}}` | `2025-01-15` | `Januar 15, 2025` | `January 15, 2025` |

Format specifiers only apply to values of the matching type: a value that is already a string (for example
`"€499.00"`) is inserted as it is. If you need a format that the specifiers cannot express, format the value in
code and pass the string. See [Format Specifiers](../for-template-authors/format-specifiers.md) for the full list.

---

## Step 8: Batch Invoice Generation

Generate multiple invoices at once. The processor can be reused (and shared between threads):

```csharp
public static void GenerateInvoices(DocumentTemplateProcessor processor, IEnumerable<Invoice> invoices)
{
    Directory.CreateDirectory("invoices");
    byte[] template = File.ReadAllBytes("invoice-template.docx");

    foreach (Invoice invoice in invoices)
    {
        var data = new Dictionary<string, object>
        {
            ["InvoiceNumber"] = invoice.InvoiceNumber,
            ["InvoiceDate"] = invoice.InvoiceDate,
            ["BillTo"] = invoice.BillTo,
            ["LineItems"] = invoice.LineItems,
            ["Subtotal"] = invoice.Subtotal,
            ["Tax"] = invoice.Tax,
            ["Total"] = invoice.Total,
            ["PaymentTerms"] = invoice.PaymentTerms
        };

        // Bytes in, bytes out: the template is read once
        ProcessingResult result = processor.ProcessTemplate(template, data, out byte[] output);

        if (result.IsSuccess)
        {
            File.WriteAllBytes($"invoices/invoice-{invoice.InvoiceNumber}.docx", output);
            Console.WriteLine($"✓ Generated: {invoice.InvoiceNumber}");
        }
        else
        {
            Console.WriteLine($"✗ Failed: {invoice.InvoiceNumber} - {result.ErrorMessage}");
        }
    }
}
```

---

## Complete Production-Ready Example

```csharp
using System.Globalization;
using TriasDev.Templify.Core;

public class InvoiceGenerator
{
    private readonly DocumentTemplateProcessor _processor;
    private readonly string _templatePath;

    public InvoiceGenerator(string templatePath)
    {
        _processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.GetCultureInfo("de-DE")
        });
        _templatePath = templatePath;
    }

    public bool GenerateInvoice(Invoice invoice, string outputPath)
    {
        var data = new Dictionary<string, object>
        {
            ["InvoiceNumber"] = invoice.InvoiceNumber,
            ["InvoiceDate"] = invoice.InvoiceDate,
            ["BillTo"] = invoice.BillTo,
            ["LineItems"] = invoice.LineItems,
            ["Subtotal"] = invoice.Subtotal,
            ["Tax"] = invoice.Tax,
            ["Total"] = invoice.Total,
            ["PaymentTerms"] = invoice.PaymentTerms
        };

        try
        {
            // Writes the output file only when processing succeeds
            ProcessingResult result = _processor.ProcessTemplateFile(_templatePath, outputPath, data);

            if (!result.IsSuccess)
            {
                Console.WriteLine($"Invoice generation failed: {result.ErrorMessage}");
                return false;
            }

            if (result.MissingVariables.Count > 0)
            {
                Console.WriteLine($"Warning - missing variables: {string.Join(", ", result.MissingVariables)}");
            }

            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error reading or writing files: {ex.Message}");
            return false;
        }
    }
}

// Usage
var generator = new InvoiceGenerator("templates/invoice-template.docx");
Invoice invoice = InvoiceFactory.CreateSampleInvoice();

Directory.CreateDirectory("output");
if (generator.GenerateInvoice(invoice, $"output/invoice-{invoice.InvoiceNumber}.docx"))
{
    Console.WriteLine("✓ Invoice generated successfully!");
}
```

---

## What You Learned

✅ **Collections** - Working with lists and arrays
✅ **Loops** - Repeating content with `{{#foreach}}`
✅ **Loop variables** - Using `@index`, `@number`, `@first`, `@last`, `@count`
✅ **Table row loops** - Dynamic table generation with marker rows
✅ **Calculations** - Computing values in code
✅ **Formatting** - Currency, number and date format specifiers
✅ **Real-world patterns** - Production-ready invoice generation
✅ **Batch processing** - Generating multiple documents

---

## Next Steps

- **[Conditionals](../for-template-authors/conditionals.md)** - Show or hide content based on data
- **[Loops](../for-template-authors/loops.md)** - Nested loops, named iteration variables, table loops in detail
- **[Boolean Expressions](../for-template-authors/boolean-expressions.md)** - Logic inside placeholders

---

## Additional Resources

- [Examples Collection](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/Examples.md) - loops and tables
- [FAQ - Loops](../FAQ.md#q-how-do-loops-work)
- [FAQ - Loops in Tables](../FAQ.md#q-how-do-i-use-loops-in-tables)

---

**Happy invoicing!** 💰
