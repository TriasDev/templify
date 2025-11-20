using DocumentFormat.OpenXml.Packaging;

namespace TriasDev.Templify.DocumentGenerator.Generators;

/// <summary>
/// Generates an invoice example demonstrating table loops and calculations
/// </summary>
public class InvoiceGenerator : BaseExampleGenerator
{
    public override string Name => "invoice";

    public override string Description => "Invoice with table loops, line items, and calculations";

    public override string GenerateTemplate(string outputDirectory)
    {
        var templatePath = Path.Combine(outputDirectory, $"{Name}-template.docx");

        using (var doc = CreateDocument(templatePath))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            // Invoice Header
            AddParagraph(body, "INVOICE", isBold: true);
            AddEmptyParagraph(body);

            // Invoice Information
            AddParagraph(body, "Invoice #: {{InvoiceNumber}}");
            AddParagraph(body, "Date: {{InvoiceDate}}");
            AddParagraph(body, "Due Date: {{DueDate}}");
            AddEmptyParagraph(body);

            // Company Information
            AddParagraph(body, "From:", isBold: true);
            AddParagraph(body, "{{Company.Name}}");
            AddParagraph(body, "{{Company.Address}}");
            AddParagraph(body, "{{Company.City}}, {{Company.Zip}}");
            AddParagraph(body, "{{Company.Email}}");
            AddEmptyParagraph(body);

            // Customer Information
            AddParagraph(body, "Bill To:", isBold: true);
            AddParagraph(body, "{{Customer.Name}}");
            AddParagraph(body, "{{Customer.Address}}");
            AddParagraph(body, "{{Customer.City}}, {{Customer.Zip}}");
            AddEmptyParagraph(body);

            // Line Items Table with Loop
            AddParagraph(body, "Line Items:", isBold: true);

            var table = CreateTable(5);
            AddTableHeaderRow(table, "Description", "Quantity", "Unit Price", "Tax Rate", "Total");

            // Loop markers and data row (three separate rows)
            AddTableRow(table, "{{#foreach Items}}", "", "", "", "");  // Loop start marker
            AddTableRow(table, "{{Description}}", "{{Quantity}}", "{{UnitPrice}}", "{{TaxRate}}", "{{Total}}");  // Data row
            AddTableRow(table, "{{/foreach}}", "", "", "", "");  // Loop end marker

            body.AppendChild(table);
            AddEmptyParagraph(body);

            // Totals
            AddParagraph(body, "Subtotal: {{Subtotal}}");
            AddParagraph(body, "Tax: {{Tax}}");
            AddParagraph(body, "Total Amount: {{TotalAmount}}", isBold: true);
            AddEmptyParagraph(body);

            // Payment Terms
            AddParagraph(body, "Payment Terms:", isBold: true);
            AddParagraph(body, "{{PaymentTerms}}");
            AddEmptyParagraph(body);

            // Thank you message
            AddParagraph(body, "Thank you for your business!");

            doc.Save();
        }

        return templatePath;
    }

    public override Dictionary<string, object> GetSampleData()
    {
        var items = new List<Dictionary<string, object>>
        {
            new()
            {
                ["Description"] = "Web Development Services",
                ["Quantity"] = "40",
                ["UnitPrice"] = "$150.00",
                ["TaxRate"] = "19%",
                ["Total"] = "$7,140.00"
            },
            new()
            {
                ["Description"] = "UI/UX Design",
                ["Quantity"] = "20",
                ["UnitPrice"] = "$120.00",
                ["TaxRate"] = "19%",
                ["Total"] = "$2,856.00"
            },
            new()
            {
                ["Description"] = "Hosting Services (1 year)",
                ["Quantity"] = "1",
                ["UnitPrice"] = "$500.00",
                ["TaxRate"] = "19%",
                ["Total"] = "$595.00"
            }
        };

        return new Dictionary<string, object>
        {
            ["InvoiceNumber"] = "INV-2025-001",
            ["InvoiceDate"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["DueDate"] = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd"),
            ["Company"] = new Dictionary<string, object>
            {
                ["Name"] = "TriasDev GmbH & Co. KG",
                ["Address"] = "123 Business Street",
                ["City"] = "Berlin",
                ["Zip"] = "10115",
                ["Email"] = "contact@triasdev.com"
            },
            ["Customer"] = new Dictionary<string, object>
            {
                ["Name"] = "Acme Corporation",
                ["Address"] = "456 Enterprise Ave",
                ["City"] = "Munich",
                ["Zip"] = "80331"
            },
            ["Items"] = items,
            ["Subtotal"] = "$8,820.00",
            ["Tax"] = "$1,675.80",
            ["TotalAmount"] = "$10,591.00",
            ["PaymentTerms"] = "Payment due within 30 days. Late payments may incur a 5% monthly interest charge."
        };
    }
}
