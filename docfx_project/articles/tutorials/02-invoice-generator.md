# Tutorial 2: Building an Invoice Generator

**Duration**: 1 hour
**Difficulty**: Intermediate
**Prerequisites**: [Tutorial 1: Hello World](01-hello-world.md) completed

---

## What You'll Learn

- Working with collections (lists, arrays)
- Using loops to repeat content
- Loop special variables (`@index`, `@first`, `@last`, `@count`)
- Creating dynamic tables with row loops
- Combining nested data with loops
- Building a real-world invoice template

---

## The Goal

Build a complete invoice generator that produces professional invoices like this:

```
INVOICE #INV-2025-001                          Date: January 15, 2025

Bill To:                                      Ship To:
Acme Corporation                              Acme Warehouse
123 Main Street                               456 Storage Lane
Springfield, IL 62701                         Springfield, IL 62702

Line Items:
┌────┬──────────────────────┬─────────┬──────────┬───────────┐
│ #  │ Description          │ Qty     │ Price    │ Total     │
├────┼──────────────────────┼─────────┼──────────┼───────────┤
│ 1  │ Software License     │ 5       │ €499.00  │ €2,495.00 │
│ 2  │ Support & Maintenance│ 5       │ €99.00   │ €495.00   │
│ 3  │ Training Package     │ 2       │ €250.00  │ €500.00   │
└────┴──────────────────────┴─────────┴──────────┴───────────┘

                                              Subtotal: €3,490.00
                                                   Tax: €663.10
                                                 Total: €4,153.10

Payment due within 30 days.
```

---

## Step 1: Understanding Loops

Before building the invoice, let's understand how loops work.

### Basic Loop Syntax

**Template**:
```
{{#foreach Items}}
- {{Name}}
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

**Output**:
```
Laptop: 999.00 EUR
Mouse: 29.00m EUR
Keyboard: 79.00 EUR
```

---

## Step 2: Loop Special Variables

Inside loops, you have access to special variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `@index` | Current position (0-based) | 0, 1, 2, ... |
| `@first` | True for first item | true, false, false, ... |
| `@last` | True for last item | false, false, true |
| `@count` | Total number of items | 3, 3, 3, ... |

**Template Example**:
```
{{#foreach Items}}
Item {{@index}}: {{Name}}{{#if @last}} (final item){{/if}}
{{/foreach}}

Total items: {{@count}}
```

---

## Step 3: Create the Invoice Template

Open Word and create `invoice-template.docx`:

```
INVOICE #{{InvoiceNumber}}                    Date: {{InvoiceDate}}

Bill To:
{{BillTo.CompanyName}}
{{BillTo.Street}}
{{BillTo.City}}, {{BillTo.State}} {{BillTo.Zip}}

Line Items:
```

Now create a table with this structure:

| # | Description | Quantity | Unit Price | Total |
|---|-------------|----------|------------|-------|
| {{#foreach LineItems}} | | | | |
| {{Position}} | {{Description}} | {{Quantity}} | {{UnitPrice}} | {{LineTotal}} |
| {{/foreach}} | | | | |

After the table, add:

```
                                              Subtotal: {{Subtotal}}
                                                   Tax: {{Tax}}
                                                 Total: {{Total}}

{{PaymentTerms}}
```

---

## Step 4: Define Data Classes

```csharp
public class Invoice
{
    public string InvoiceNumber { get; set; }
    public string InvoiceDate { get; set; }
    public Address BillTo { get; set; }
    public List<LineItem> LineItems { get; set; }
    public string Subtotal { get; set; }
    public string Tax { get; set; }
    public string Total { get; set; }
    public string PaymentTerms { get; set; }
}

public class Address
{
    public string CompanyName { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
}

public class LineItem
{
    public int Position { get; set; }
    public string Description { get; set; }
    public int Quantity { get; set; }
    public string UnitPrice { get; set; }
    public string LineTotal { get; set; }
}
```

---

## Step 5: Generate Invoice Data

```csharp
public static Invoice CreateSampleInvoice()
{
    var lineItems = new List<LineItem>
    {
        new LineItem
        {
            Position = 1,
            Description = "Software Enterprise License",
            Quantity = 5,
            UnitPrice = "€499.00",
            LineTotal = "€2,495.00"
        },
        new LineItem
        {
            Position = 2,
            Description = "Support & Maintenance (Annual)",
            Quantity = 5,
            UnitPrice = "€99.00",
            LineTotal = "€495.00"
        },
        new LineItem
        {
            Position = 3,
            Description = "Training Package (2 days)",
            Quantity = 2,
            UnitPrice = "€250.00",
            LineTotal = "€500.00"
        }
    };

    return new Invoice
    {
        InvoiceNumber = "INV-2025-001",
        InvoiceDate = DateTime.Now.ToString("MMMM dd, yyyy"),
        BillTo = new Address
        {
            CompanyName = "Acme Corporation",
            Street = "123 Main Street",
            City = "Springfield",
            State = "IL",
            Zip = "62701"
        },
        LineItems = lineItems,
        Subtotal = "€3,490.00",
        Tax = "€663.10",
        Total = "€4,153.10",
        PaymentTerms = "Payment due within 30 days. Thank you for your business!"
    };
}
```

---

## Step 6: Process the Invoice

```csharp
using TriasDev.Templify;

public class Program
{
    public static void Main()
    {
        var invoice = CreateSampleInvoice();

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

        var processor = new DocumentTemplateProcessor();

        using var templateStream = File.OpenRead("invoice-template.docx");
        using var outputStream = File.Create($"invoice-{invoice.InvoiceNumber}.docx");

        var result = processor.ProcessTemplate(templateStream, outputStream, data);

        if (result.IsSuccessful)
        {
            Console.WriteLine($"✓ Invoice {invoice.InvoiceNumber} generated!");
            Console.WriteLine($"  Line items: {invoice.LineItems.Count}");
            Console.WriteLine($"  Total: {invoice.Total}");
        }
        else
        {
            Console.WriteLine($"✗ Error: {string.Join(", ", result.Errors)}");
        }
    }
}
```

---

## Step 7: Adding Calculations

For real-world use, calculate values in code:

```csharp
public class InvoiceCalculator
{
    public static Invoice CreateInvoice(List<LineItem> items)
    {
        decimal subtotal = 0;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            item.Position = i + 1;

            // Calculate line total
            decimal lineTotal = item.QuantityValue * item.UnitPriceValue;
            item.LineTotal = FormatCurrency(lineTotal);

            subtotal += lineTotal;
        }

        decimal taxRate = 0.19m; // 19% VAT
        decimal tax = subtotal * taxRate;
        decimal total = subtotal + tax;

        return new Invoice
        {
            InvoiceNumber = GenerateInvoiceNumber(),
            InvoiceDate = DateTime.Now.ToString("MMMM dd, yyyy"),
            LineItems = items,
            Subtotal = FormatCurrency(subtotal),
            Tax = FormatCurrency(tax),
            Total = FormatCurrency(total),
            PaymentTerms = "Payment due within 30 days."
        };
    }

    private static string GenerateInvoiceNumber()
    {
        return $"INV-{DateTime.Now:yyyy}-{Random.Shared.Next(1000, 9999)}";
    }

    private static string FormatCurrency(decimal amount)
    {
        return amount.ToString("C", CultureInfo.GetCultureInfo("de-DE")); // €1.234,56
    }
}

// Updated LineItem class with decimal properties
public class LineItem
{
    public int Position { get; set; }
    public string Description { get; set; }
    public int QuantityValue { get; set; }
    public decimal UnitPriceValue { get; set; }

    // Formatted strings for template
    public string Quantity => QuantityValue.ToString();
    public string UnitPrice { get; set; }
    public string LineTotal { get; set; }
}
```

---

## Step 8: Batch Invoice Generation

Generate multiple invoices at once:

```csharp
public static void GenerateInvoicesForCustomers(List<Customer> customers)
{
    var processor = new DocumentTemplateProcessor();

    foreach (var customer in customers)
    {
        try
        {
            var invoice = CreateInvoiceForCustomer(customer);

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

            using var templateStream = File.OpenRead("invoice-template.docx");
            var outputPath = $"invoices/invoice-{invoice.InvoiceNumber}.docx");
            using var outputStream = File.Create(outputPath);

            var result = processor.ProcessTemplate(templateStream, outputStream, data);

            if (result.IsSuccessful)
            {
                Console.WriteLine($"✓ Generated: {invoice.InvoiceNumber}");
            }
            else
            {
                Console.WriteLine($"✗ Failed: {customer.Name} - {string.Join(", ", result.Errors)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error for {customer.Name}: {ex.Message}");
        }
    }
}
```

---

## Complete Production-Ready Example

```csharp
using System.Globalization;
using TriasDev.Templify;

public class InvoiceGenerator
{
    private readonly DocumentTemplateProcessor _processor;
    private readonly string _templatePath;

    public InvoiceGenerator(string templatePath)
    {
        _processor = new DocumentTemplateProcessor();
        _templatePath = templatePath;
    }

    public bool GenerateInvoice(Invoice invoice, string outputPath)
    {
        try
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

            using var templateStream = File.OpenRead(_templatePath);
            using var outputStream = File.Create(outputPath);

            var result = _processor.ProcessTemplate(templateStream, outputStream, data);

            if (!result.IsSuccessful)
            {
                Console.WriteLine($"Invoice generation failed: {string.Join(", ", result.Errors)}");
                return false;
            }

            if (result.MissingVariables.Any())
            {
                Console.WriteLine($"Warning - missing variables: {string.Join(", ", result.MissingVariables)}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating invoice: {ex.Message}");
            return false;
        }
    }
}

// Usage
var generator = new InvoiceGenerator("templates/invoice-template.docx");
var invoice = InvoiceCalculator.CreateInvoice(GetLineItems());

if (generator.GenerateInvoice(invoice, $"output/invoice-{invoice.InvoiceNumber}.docx"))
{
    Console.WriteLine($"✓ Invoice generated successfully!");
}
```

---

## What You Learned

✅ **Collections** - Working with lists and arrays
✅ **Loops** - Repeating content with `{{#foreach}}`
✅ **Loop variables** - Using `@index`, `@first`, `@last`, `@count`
✅ **Table row loops** - Dynamic table generation
✅ **Calculations** - Computing values in code
✅ **Formatting** - Currency and number formatting
✅ **Real-world patterns** - Production-ready invoice generation
✅ **Batch processing** - Generating multiple documents

---

## Next Steps

- **[Tutorial 3: Conditionals & Loops](03-conditionals-and-loops.md)** - Master dynamic content with conditions
- **[Tutorial 4: Advanced Features](04-advanced-features.md)** - Nested loops, complex expressions, optimization

---

## Additional Resources

- [Loop Examples](../../TriasDev.Templify/Examples.md#loops)
- [Table Examples](../../TriasDev.Templify/Examples.md#tables)
- [FAQ - Loops](../FAQ.md#loops)

---

**Happy invoicing!** 💰
