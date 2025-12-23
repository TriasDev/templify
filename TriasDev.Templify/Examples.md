# Templify Examples

This document provides practical examples for common use cases.

## Table of Contents

1. [Basic Variable Replacement](#basic-variable-replacement)
2. [Working with Different Data Types](#working-with-different-data-types)
3. [Markdown Formatting](#markdown-formatting)
4. [Table Replacement](#table-replacement)
5. [Loops and Iterations](#loops-and-iterations)
6. [Nested Loops](#nested-loops)
7. [Loop Metadata](#loop-metadata)
8. [Lists in Loops](#lists-in-loops)
9. [Conditional Blocks](#conditional-blocks)
10. [Nested Conditionals](#nested-conditionals)
11. [Handling Missing Variables](#handling-missing-variables)
12. [Error Handling](#error-handling)
13. [Processing Multiple Templates](#processing-multiple-templates)
14. [Web Application Integration](#web-application-integration)
15. [Report Generation](#report-generation)

---

## Basic Variable Replacement

### Simple Invoice Generation

```csharp
using TriasDev.Templify;

// Prepare invoice data
var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = "INV-2025-001",
    ["CustomerName"] = "TriasDev GmbH & Co. KG",
    ["IssueDate"] = DateTime.Now,
    ["DueDate"] = DateTime.Now.AddDays(30),
    ["TotalAmount"] = 2499.99m
};

// Process template
var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("invoice-template.docx");
using var outputStream = File.Create("invoice-output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);

Console.WriteLine($"Invoice generated: {result.ReplacementCount} fields replaced");
```

### Template (invoice-template.docx):

```
INVOICE {{InvoiceNumber}}

Bill To: {{CustomerName}}
Issue Date: {{IssueDate}}
Due Date: {{DueDate}}

Total Amount: {{TotalAmount}} EUR
```

---

## Working with Different Data Types

### Data Type Conversion Examples

```csharp
using TriasDev.Templify;

var data = new Dictionary<string, object>
{
    // Strings - used as-is
    ["CompanyName"] = "BuildCorp Ltd",

    // Numbers - formatted with current culture
    ["EmployeeCount"] = 150,
    ["Revenue"] = 1_500_000.50m,
    ["GrowthRate"] = 12.5,

    // Dates - formatted with current culture
    ["FoundedDate"] = new DateTime(2010, 5, 15),
    ["LastAudit"] = DateTimeOffset.Now.AddMonths(-6),

    // Booleans - "True" or "False"
    ["IsPublicCompany"] = false,
    ["HasCertification"] = true,

    // Null values - handled based on options
    ["OptionalField"] = null
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("company-profile-template.docx");
using var outputStream = File.Create("company-profile.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Custom ToString() Implementation

```csharp
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }

    public override string ToString()
    {
        return $"{Street}, {PostalCode} {City}, {Country}";
    }
}

// Usage
var data = new Dictionary<string, object>
{
    ["CompanyAddress"] = new Address
    {
        Street = "Main Street 123",
        City = "Berlin",
        PostalCode = "10115",
        Country = "Germany"
    }
};

// In template: {{CompanyAddress}}
// Output: Main Street 123, 10115 Berlin, Germany
```

---

## Markdown Formatting

Templify supports markdown syntax in variable values for text formatting. This allows you to dynamically apply bold, italic, and strikethrough formatting to your documents without modifying the template.

### Basic Markdown Syntax

```csharp
using TriasDev.Templify;

// Template: {{Message}}

var data = new Dictionary<string, object>
{
    ["Message"] = "My name is **Alice**" // Bold
};

// Result: "My name is " (plain) + "Alice" (bold)
```

**Supported Markdown:**
- `**text**` or `__text__` → **Bold**
- `*text*` or `_text_` → *Italic*
- `~~text~~` → ~~Strikethrough~~
- `***text***` → ***Bold + Italic***

### Mixed Formatting

```csharp
var data = new Dictionary<string, object>
{
    ["Summary"] = "Normal **bold** and *italic* and ~~strikethrough~~ text"
};

// Result: Multiple runs with different formatting applied
```

### Markdown with Template Formatting

Markdown formatting is **merged** with existing template formatting. If your template has formatted text, markdown adds to it:

```csharp
// Template has red text: {{Message}}
var data = new Dictionary<string, object>
{
    ["Message"] = "Red text with **bold**"
};

// Result: "Red text with " (red) + "bold" (red + bold)
// The bold formatting is added to the existing red color
```

### Markdown in Loops

```csharp
var data = new Dictionary<string, object>
{
    ["Items"] = new List<string>
    {
        "Item **one** is bold",
        "Item *two* is italic",
        "Item ~~three~~ is strikethrough"
    }
};

// Template:
// {{#foreach Items}}
// - {{.}}
// {{/foreach}}

// Result: Each item rendered with appropriate formatting
```

### Complex Formatting Example

```csharp
var data = new Dictionary<string, object>
{
    ["Report"] = "Status: **Completed** | Priority: *High* | Previous status: ~~Pending~~",
    ["Notes"] = "This is ***very important*** information",
    ["Warning"] = "**Warning:** Do not modify ~~outdated~~ *deprecated* fields"
};
```

### Malformed Markdown

If markdown syntax is malformed (unclosed markers), it renders as plain text:

```csharp
var data = new Dictionary<string, object>
{
    ["Invalid"] = "Hello **world"  // Missing closing **
};

// Result: "Hello **world" (rendered literally, no formatting)
```

---

## Table Replacement

### Order Items Table

```csharp
using TriasDev.Templify;

// Note: MVP doesn't support loops, so each item needs explicit placeholders
var data = new Dictionary<string, object>
{
    // Header
    ["OrderNumber"] = "ORD-2025-001",

    // Item 1
    ["Item1Name"] = "Software Enterprise License",
    ["Item1Quantity"] = 5,
    ["Item1Price"] = 499.00m,
    ["Item1Total"] = 2495.00m,

    // Item 2
    ["Item2Name"] = "Support & Maintenance (Annual)",
    ["Item2Quantity"] = 5,
    ["Item2Price"] = 99.00m,
    ["Item2Total"] = 495.00m,

    // Totals
    ["Subtotal"] = 2990.00m,
    ["Tax"] = 568.10m,
    ["GrandTotal"] = 3558.10m
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("order-template.docx");
using var outputStream = File.Create("order.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template Table:

| Item | Quantity | Unit Price | Total |
|------|----------|------------|-------|
| {{Item1Name}} | {{Item1Quantity}} | {{Item1Price}} | {{Item1Total}} |
| {{Item2Name}} | {{Item2Quantity}} | {{Item2Price}} | {{Item2Total}} |
| | | **Subtotal** | {{Subtotal}} |
| | | **Tax (19%)** | {{Tax}} |
| | | **Grand Total** | {{GrandTotal}} |

---

## Loops and Iterations

### Invoice with Line Items Loop

```csharp
using TriasDev.Templify;

public class LineItem
{
    public int Position { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = "INV-2025-001",
    ["CompanyName"] = "TriasDev GmbH & Co. KG",
    ["CustomerName"] = "Acme Corporation",
    ["IssueDate"] = DateTime.Now,

    ["LineItems"] = new List<LineItem>
    {
        new LineItem
        {
            Position = 1,
            Product = "Software Enterprise License",
            Quantity = 5,
            UnitPrice = 499.00m,
            Total = 2495.00m
        },
        new LineItem
        {
            Position = 2,
            Product = "Support & Maintenance",
            Quantity = 5,
            UnitPrice = 99.00m,
            Total = 495.00m
        },
        new LineItem
        {
            Position = 3,
            Product = "Training Package",
            Quantity = 2,
            UnitPrice = 250.00m,
            Total = 500.00m
        }
    },

    ["Subtotal"] = 3490.00m,
    ["Tax"] = 663.10m,
    ["Total"] = 4153.10m
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("invoice-template.docx");
using var outputStream = File.Create("invoice-output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);

Console.WriteLine($"Invoice generated with {result.ReplacementCount} replacements");
```

### Template (invoice-template.docx):

```
INVOICE {{InvoiceNumber}}

From: {{CompanyName}}
To: {{CustomerName}}
Date: {{IssueDate}}

LINE ITEMS:
{{#foreach LineItems}}
{{Position}}. {{Product}}
   Quantity: {{Quantity}} @ {{UnitPrice}} EUR = {{Total}} EUR

{{/foreach}}

Subtotal: {{Subtotal}} EUR
Tax (19%): {{Tax}} EUR
Total: {{Total}} EUR
```

### Output:

```
INVOICE INV-2025-001

From: TriasDev GmbH & Co. KG
To: Acme Corporation
Date: 11/7/2025 10:30:00 AM

LINE ITEMS:
1. Software Enterprise License
   Quantity: 5 @ 499.00 EUR = 2495.00 EUR

2. Support & Maintenance
   Quantity: 5 @ 99.00 EUR = 495.00 EUR

3. Training Package
   Quantity: 2 @ 250.00 EUR = 500.00 EUR


Subtotal: 3490.00 EUR
Tax (19%): 663.10 EUR
Total: 4153.10 EUR
```

### Table Loop Example

```csharp
using TriasDev.Templify;

var data = new Dictionary<string, object>
{
    ["OrderNumber"] = "ORD-2025-001",
    ["LineItems"] = new List<LineItem>
    {
        new LineItem { Product = "Product A", Quantity = 5, UnitPrice = 99.00m, Total = 495.00m },
        new LineItem { Product = "Product B", Quantity = 3, UnitPrice = 149.00m, Total = 447.00m },
        new LineItem { Product = "Product C", Quantity = 2, UnitPrice = 249.00m, Total = 498.00m }
    },
    ["GrandTotal"] = 1440.00m
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("order-template.docx");
using var outputStream = File.Create("order-output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template Table:

| Product | Quantity | Unit Price | Total |
|---------|----------|------------|-------|
| {{#foreach LineItems}} | | | |
| {{Product}} | {{Quantity}} | {{UnitPrice}} | {{Total}} |
| {{/foreach}} | | | |
| | | **Grand Total** | {{GrandTotal}} |

---

## Nested Loops

### Orders with Items

```csharp
using TriasDev.Templify;

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total { get; set; }
}

public class OrderItem
{
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

var data = new Dictionary<string, object>
{
    ["ReportTitle"] = "Monthly Orders Report",
    ["Orders"] = new List<Order>
    {
        new Order
        {
            OrderId = "ORD-001",
            CustomerName = "Acme Corporation",
            OrderDate = new DateTime(2025, 11, 1),
            Items = new List<OrderItem>
            {
                new OrderItem { Product = "Product A", Quantity = 2, Price = 99.00m },
                new OrderItem { Product = "Product B", Quantity = 1, Price = 149.00m }
            },
            Total = 347.00m
        },
        new Order
        {
            OrderId = "ORD-002",
            CustomerName = "BuildCorp Ltd",
            OrderDate = new DateTime(2025, 11, 5),
            Items = new List<OrderItem>
            {
                new OrderItem { Product = "Product C", Quantity = 5, Price = 49.00m }
            },
            Total = 245.00m
        }
    }
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("orders-report-template.docx");
using var outputStream = File.Create("orders-report.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
{{ReportTitle}}

{{#foreach Orders}}
Order: {{OrderId}}
Customer: {{CustomerName}}
Date: {{OrderDate}}

Items:
{{#foreach Items}}
  - {{Product}} (Qty: {{Quantity}}) @ {{Price}} EUR
{{/foreach}}

Order Total: {{Total}} EUR
─────────────────────────────────────

{{/foreach}}
```

---

## Loop Metadata

### Using @index, @first, @last, @count

```csharp
using TriasDev.Templify;

public class Task
{
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
}

var data = new Dictionary<string, object>
{
    ["ProjectName"] = "Software Development",
    ["Tasks"] = new List<Task>
    {
        new Task { Title = "Design database schema", Status = "Completed", Assignee = "Alice" },
        new Task { Title = "Implement API endpoints", Status = "In Progress", Assignee = "Bob" },
        new Task { Title = "Write documentation", Status = "Pending", Assignee = "Charlie" },
        new Task { Title = "Deploy to staging", Status = "Pending", Assignee = "Diana" }
    }
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("task-report-template.docx");
using var outputStream = File.Create("task-report.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
Project: {{ProjectName}}

Task List (Total: {{Tasks.@count}} tasks):

{{#foreach Tasks}}
Task {{@index}}: {{Title}}
Status: {{Status}}
Assigned to: {{Assignee}}
{{#if @last}}
(This is the last task)
{{/if}}

{{/foreach}}
```

### Output:

```
Project: Software Development

Task List (Total: 4 tasks):

Task 0: Design database schema
Status: Completed
Assigned to: Alice

Task 1: Implement API endpoints
Status: In Progress
Assigned to: Bob

Task 2: Write documentation
Status: Pending
Assigned to: Charlie

Task 3: Deploy to staging
Status: Pending
Assigned to: Diana
(This is the last task)
```

---

## Lists in Loops

Templify fully preserves Word's bullet and numbered list formatting when used inside `{{#foreach}}` loops. This allows you to create dynamic lists with proper formatting.

### Bullet List with Simple Strings

```csharp
using TriasDev.Templify;

Dictionary<string, object> data = new Dictionary<string, object>
{
    ["Features"] = new List<string>
    {
        "Advanced GDPR compliance tracking",
        "Automated risk assessments",
        "Real-time reporting and analytics",
        "Multi-language support"
    }
};

DocumentTemplateProcessor processor = new DocumentTemplateProcessor();

using FileStream templateStream = File.OpenRead("features-template.docx");
using FileStream outputStream = File.Create("features-output.docx");

ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template (features-template.docx):

```
Product Features:

{{#foreach Features}}
• {{.}}
{{/foreach}}
```

**Important:** The bullet point (•) must be a real Word bullet list item created using Word's bullet list formatting (Home → Bullets), not just a bullet character typed as text.

### Output:

```
Product Features:

• Advanced GDPR compliance tracking
• Automated risk assessments
• Real-time reporting and analytics
• Multi-language support
```

Each item is rendered as a properly formatted bullet list item in Word.

### Numbered List with Simple Strings

```csharp
Dictionary<string, object> data = new Dictionary<string, object>
{
    ["Steps"] = new List<string>
    {
        "Download and install the application",
        "Configure your organization settings",
        "Import existing data",
        "Train your team",
        "Start using the system"
    }
};
```

### Template (setup-template.docx):

```
Setup Steps:

{{#foreach Steps}}
1. {{.}}
{{/foreach}}
```

**Important:** Use Word's numbered list formatting (Home → Numbering), not manual numbers.

### Output:

```
Setup Steps:

1. Download and install the application
2. Configure your organization settings
3. Import existing data
4. Train your team
5. Start using the system
```

The numbers are automatically incremented by Word's list formatting.

### Bullet List with Object Properties

```csharp
public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool InStock { get; set; }
}

Dictionary<string, object> data = new Dictionary<string, object>
{
    ["Products"] = new List<Product>
    {
        new Product { Name = "Enterprise Edition", Price = 999.00m, InStock = true },
        new Product { Name = "Professional Edition", Price = 499.00m, InStock = true },
        new Product { Name = "Starter Edition", Price = 199.00m, InStock = true }
    }
};
```

### Template (products-template.docx):

```
Available Products:

{{#foreach Products}}
• {{Name}} - {{Price}} EUR
{{/foreach}}
```

Again, the bullet point must be created using Word's bullet list formatting.

### Output:

```
Available Products:

• Enterprise Edition - 999.00 EUR
• Professional Edition - 499.00 EUR
• Starter Edition - 199.00 EUR
```

### How It Works

When the template processor encounters a `{{#foreach}}` loop, it:
1. Detects the loop boundaries
2. Clones the content between loop markers (including paragraphs with list formatting)
3. Preserves all paragraph properties, including **NumberingProperties** (which control list formatting)
4. Creates one copy per item in the collection
5. Replaces placeholders in each copy

This means any Word formatting applied to paragraphs—including bullets, numbering, indentation, and styles—is automatically preserved.

### Current Limitations

- **Nested foreach loops with lists**: Simple nested loops work, but the inner loop items might not maintain proper list indentation in all scenarios.

---

## Conditional Blocks

### Simple Conditional with If/Else

```csharp
using TriasDev.Templify;

public class Customer
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public bool IsVIP { get; set; }
}

var data = new Dictionary<string, object>
{
    ["CustomerName"] = "Acme Corporation",
    ["OrderTotal"] = 2500.00m,
    ["IsVIP"] = true,
    ["HasActiveSubscription"] = true,
    ["Status"] = "Active",
    ["CreditLimit"] = 50000.00m
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("customer-report-template.docx");
using var outputStream = File.Create("customer-report.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
CUSTOMER REPORT

Customer: {{CustomerName}}

{{#if IsVIP}}
⭐ VIP CUSTOMER ⭐
Special Benefits Applied
{{#else}}
Standard Customer
{{/if}}

Account Status:
{{#if Status = "Active"}}
✓ Account is active and in good standing
{{#else}}
⚠ Account requires attention
{{/if}}

{{#if HasActiveSubscription}}
Subscription: Active
Access Level: Full Platform Access
{{#else}}
Subscription: Inactive
Please contact sales to renew
{{/if}}
```

### Output:

```
CUSTOMER REPORT

Customer: Acme Corporation

⭐ VIP CUSTOMER ⭐
Special Benefits Applied

Account Status:
✓ Account is active and in good standing

Subscription: Active
Access Level: Full Platform Access
```

---

### Conditional with Comparison Operators

```csharp
using TriasDev.Templify;

public class OrderInfo
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public string ShippingCountry { get; set; } = string.Empty;
}

var data = new Dictionary<string, object>
{
    ["OrderId"] = "ORD-2025-001",
    ["Total"] = 1500.00m,
    ["ItemCount"] = 12,
    ["ShippingCountry"] = "Germany",
    ["IsPriorityShipping"] = true
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("order-confirmation-template.docx");
using var outputStream = File.Create("order-confirmation.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
ORDER CONFIRMATION
Order ID: {{OrderId}}

{{#if Total > 1000}}
🎉 Congratulations! You qualify for FREE SHIPPING!
{{/if}}

{{#if Total > 500 and Total < 1000}}
You're close to free shipping! Add ${{1000 - Total}} more.
{{/if}}

{{#if ItemCount > 10}}
Bulk Order Discount Applied: 10%
{{/if}}

Shipping Information:
{{#if ShippingCountry = "Germany"}}
Estimated Delivery: 2-3 business days
Shipping Cost: FREE
{{#else}}
Estimated Delivery: 5-7 business days
Shipping Cost: Calculated at checkout
{{/if}}

{{#if IsPriorityShipping}}
⚡ PRIORITY SHIPPING SELECTED
Your order will be shipped within 24 hours
{{/if}}
```

### Output:

```
ORDER CONFIRMATION
Order ID: ORD-2025-001

🎉 Congratulations! You qualify for FREE SHIPPING!

Bulk Order Discount Applied: 10%

Shipping Information:
Estimated Delivery: 2-3 business days
Shipping Cost: FREE

⚡ PRIORITY SHIPPING SELECTED
Your order will be shipped within 24 hours
```

---

### Conditional with ElseIf Branches

Use `{{#elseif condition}}` to create multi-branch conditionals for scenarios like grade calculations, status displays, or tiered pricing.

```csharp
using TriasDev.Templify;

var data = new Dictionary<string, object>
{
    ["StudentName"] = "Alice Johnson",
    ["Score"] = 75
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("grade-report-template.docx");
using var outputStream = File.Create("grade-report.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
GRADE REPORT

Student: {{StudentName}}
Score: {{Score}}

{{#if Score >= 90}}
Grade: A - Excellent!
{{#elseif Score >= 80}}
Grade: B - Good
{{#elseif Score >= 70}}
Grade: C - Satisfactory
{{#elseif Score >= 60}}
Grade: D - Needs Improvement
{{#else}}
Grade: F - Please see instructor
{{/if}}
```

### Output:

```
GRADE REPORT

Student: Alice Johnson
Score: 75

Grade: C - Satisfactory
```

**Note:** The `{{#else}}` branch must always be last. Placing `{{#elseif}}` after `{{#else}}` will result in an error.

---

### Conditional with Loops

```csharp
using TriasDev.Templify;

public class Invoice
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public List<InvoiceItem> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
}

public class InvoiceItem
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = "INV-2025-001",
    ["CustomerName"] = "TriasDev GmbH & Co. KG",
    ["HasDiscount"] = true,
    ["DiscountPercent"] = 15,
    ["Items"] = new List<InvoiceItem>
    {
        new InvoiceItem { Description = "Software License", Quantity = 5, UnitPrice = 499.00m, Total = 2495.00m },
        new InvoiceItem { Description = "Support Package", Quantity = 5, UnitPrice = 99.00m, Total = 495.00m }
    },
    ["Subtotal"] = 2990.00m,
    ["Discount"] = 448.50m,
    ["Tax"] = 483.19m,
    ["Total"] = 3024.69m
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("invoice-template.docx");
using var outputStream = File.Create("invoice.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
INVOICE {{InvoiceNumber}}

Bill To: {{CustomerName}}

LINE ITEMS:
{{#foreach Items}}
{{Description}}
Quantity: {{Quantity}} @ {{UnitPrice}} EUR = {{Total}} EUR
{{/foreach}}

Subtotal: {{Subtotal}} EUR

{{#if HasDiscount}}
Discount ({{DiscountPercent}}%): -{{Discount}} EUR
{{/if}}

Tax (19%): {{Tax}} EUR
───────────────────────────
TOTAL: {{Total}} EUR

{{#if Total > 2500}}
⭐ Thank you for your business! As a valued customer,
you'll receive priority support for this order.
{{/if}}
```

### Output:

```
INVOICE INV-2025-001

Bill To: TriasDev GmbH & Co. KG

LINE ITEMS:
Software License
Quantity: 5 @ 499.00 EUR = 2495.00 EUR
Support Package
Quantity: 5 @ 99.00 EUR = 495.00 EUR

Subtotal: 2990.00 EUR

Discount (15%): -448.50 EUR

Tax (19%): 483.19 EUR
───────────────────────────
TOTAL: 3024.69 EUR

⭐ Thank you for your business! As a valued customer,
you'll receive priority support for this order.
```

---

## Nested Conditionals

### Multi-Level Decision Tree

```csharp
using TriasDev.Templify;

public class CustomerAccount
{
    public string Name { get; set; } = string.Empty;
    public bool IsVIP { get; set; }
    public bool HasActiveSubscription { get; set; }
    public string SubscriptionTier { get; set; } = string.Empty;
    public decimal AccountBalance { get; set; }
    public decimal OrderTotal { get; set; }
}

var data = new Dictionary<string, object>
{
    ["CustomerName"] = "GlobalTech Inc",
    ["IsVIP"] = true,
    ["HasActiveSubscription"] = true,
    ["SubscriptionTier"] = "Premium",
    ["AccountBalance"] = 5000.00m,
    ["OrderTotal"] = 2500.00m,
    ["PaymentMethod"] = "Credit",
    ["ShippingCountry"] = "Germany"
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("account-summary-template.docx");
using var outputStream = File.Create("account-summary.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
ACCOUNT SUMMARY

Customer: {{CustomerName}}

{{#if IsVIP}}
═══════════════════════════════
        VIP CUSTOMER
═══════════════════════════════

{{#if HasActiveSubscription}}
Subscription Status: Active
Tier: {{SubscriptionTier}}

Benefits:
{{#if SubscriptionTier = "Premium"}}
  ✓ 24/7 Priority Support
  ✓ Free Shipping Worldwide
  ✓ 20% Discount on All Orders
  ✓ Early Access to New Features
  ✓ Dedicated Account Manager
{{#else}}
  {{#if SubscriptionTier = "Professional"}}
  ✓ Business Hours Support
  ✓ Free Shipping (orders over €100)
  ✓ 15% Discount on All Orders
  ✓ Early Access to New Features
  {{#else}}
  ✓ Standard Support
  ✓ Free Shipping (orders over €200)
  ✓ 10% Discount on All Orders
  {{/if}}
{{/if}}

{{#else}}
⚠ VIP Subscription Expired
Your VIP benefits are currently inactive.
Contact sales to renew: sales@example.com
{{/if}}

{{#if OrderTotal > 1000}}
Current Order: {{OrderTotal}} EUR

{{#if AccountBalance > OrderTotal}}
  ✓ Sufficient account balance
  Your order can be processed immediately
{{#else}}
  ⚠ Insufficient account balance
  Please add funds or choose alternative payment
{{/if}}
{{/if}}

{{#else}}
───────────────────────────────
      STANDARD CUSTOMER
───────────────────────────────

{{#if OrderTotal > 500}}
You're close to VIP status!
Reach €5000 total purchases to unlock VIP benefits.
{{/if}}

{{#if ShippingCountry = "Germany"}}
Shipping: €5.90 (orders under €50)
Free shipping on orders over €50
{{#else}}
International Shipping: Calculated at checkout
{{/if}}

{{/if}}
```

### Output:

```
ACCOUNT SUMMARY

Customer: GlobalTech Inc

═══════════════════════════════
        VIP CUSTOMER
═══════════════════════════════

Subscription Status: Active
Tier: Premium

Benefits:
  ✓ 24/7 Priority Support
  ✓ Free Shipping Worldwide
  ✓ 20% Discount on All Orders
  ✓ Early Access to New Features
  ✓ Dedicated Account Manager

Current Order: 2500.00 EUR

  ✓ Sufficient account balance
  Your order can be processed immediately
```

---

### Complex Business Logic Example

```csharp
using TriasDev.Templify;

public class ContractInfo
{
    public string ContractId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int DurationMonths { get; set; }
    public bool RequiresApproval { get; set; }
    public string ApprovalLevel { get; set; } = string.Empty;
}

var data = new Dictionary<string, object>
{
    ["ContractId"] = "CT-2025-042",
    ["ClientName"] = "SecureIT GmbH",
    ["ContractType"] = "Enterprise",
    ["ContractValue"] = 150000.00m,
    ["Duration"] = 36,
    ["RequiresLegalReview"] = true,
    ["RequiresExecutiveApproval"] = true,
    ["IsRenewal"] = false,
    ["HasCustomTerms"] = true,
    ["RiskLevel"] = "Medium"
};

var processor = new DocumentTemplateProcessor();

using var templateStream = File.OpenRead("contract-approval-template.docx");
using var outputStream = File.Create("contract-approval.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

### Template:

```
CONTRACT APPROVAL WORKFLOW
Contract ID: {{ContractId}}
Client: {{ClientName}}

CONTRACT CLASSIFICATION:
{{#if ContractType = "Enterprise"}}
Type: Enterprise Contract
Value: €{{ContractValue}}

{{#if ContractValue > 100000}}
  ⚠ HIGH-VALUE CONTRACT

  Required Approvals:
  {{#if ContractValue > 500000}}
    ☐ Board Approval Required
    ☐ CFO Approval Required
    ☐ Legal Review Required
  {{#else}}
    {{#if ContractValue > 250000}}
      ☐ Executive Approval Required
      ☐ Legal Review Required
    {{#else}}
      ☐ Department Head Approval Required
      {{#if RequiresLegalReview}}
      ☐ Legal Review Required
      {{/if}}
    {{/if}}
  {{/if}}

  {{#if Duration > 24}}
  ⚠ Long-term Commitment ({{Duration}} months)
  Extended contract requires additional due diligence
  {{/if}}

{{#else}}
  Standard Enterprise Contract
  ☐ Manager Approval Required
{{/if}}

{{#else}}
  {{#if ContractType = "Professional"}}
  Type: Professional Services Contract
  ☐ Team Lead Approval Required
  {{#else}}
  Type: Standard Contract
  ☐ Automated Approval
  {{/if}}
{{/if}}

{{#if IsRenewal}}
CONTRACT STATUS: Renewal
{{#if HasCustomTerms}}
⚠ Terms Modified - Requires Re-review
{{#else}}
✓ Standard Renewal - Expedited Process
{{/if}}
{{#else}}
CONTRACT STATUS: New Contract
Full approval workflow required
{{/if}}

RISK ASSESSMENT:
{{#if RiskLevel = "High"}}
🔴 HIGH RISK - Enhanced due diligence required
{{#else}}
  {{#if RiskLevel = "Medium"}}
  🟡 MEDIUM RISK - Standard review process
  {{#else}}
  🟢 LOW RISK - Fast-track approved
  {{/if}}
{{/if}}
```

### Output:

```
CONTRACT APPROVAL WORKFLOW
Contract ID: CT-2025-042
Client: SecureIT GmbH

CONTRACT CLASSIFICATION:
Type: Enterprise Contract
Value: €150000.00

  ⚠ HIGH-VALUE CONTRACT

  Required Approvals:
      ☐ Department Head Approval Required
      ☐ Legal Review Required

  ⚠ Long-term Commitment (36 months)
  Extended contract requires additional due diligence

CONTRACT STATUS: New Contract
Full approval workflow required

RISK ASSESSMENT:
  🟡 MEDIUM RISK - Standard review process
```

---

## Handling Missing Variables

### Option 1: Leave Unchanged (Default)

```csharp
var data = new Dictionary<string, object>
{
    ["CustomerName"] = "Acme Corporation"
    // Note: "ContactEmail" is NOT provided
};

var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
};

var processor = new DocumentTemplateProcessor(options);

using var templateStream = File.OpenRead("template.docx");
using var outputStream = File.Create("output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);

// In document:
// Customer: Acme Corporation
// Email: {{ContactEmail}}  ← Left as-is
```

### Option 2: Replace with Empty

```csharp
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
};

var processor = new DocumentTemplateProcessor(options);

// Same data and template as above

// In document:
// Customer: Acme Corporation
// Email:   ← Placeholder removed
```

### Option 3: Throw Exception

```csharp
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ThrowException
};

var processor = new DocumentTemplateProcessor(options);

try
{
    var result = processor.ProcessTemplate(templateStream, outputStream, data);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    // Output: Error: Missing variable: ContactEmail
}
```

---

## Error Handling

### Comprehensive Error Handling Example

```csharp
using TriasDev.Templify;

public class DocumentGenerator
{
    private readonly DocumentTemplateProcessor _processor;

    public DocumentGenerator()
    {
        var options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
        };

        _processor = new DocumentTemplateProcessor(options);
    }

    public (bool Success, string Message) GenerateDocument(
        string templatePath,
        string outputPath,
        Dictionary<string, object> data)
    {
        try
        {
            // Validate inputs
            if (!File.Exists(templatePath))
            {
                return (false, $"Template not found: {templatePath}");
            }

            // Process template
            using var templateStream = File.OpenRead(templatePath);
            using var outputStream = File.Create(outputPath);

            var result = _processor.ProcessTemplate(templateStream, outputStream, data);

            if (!result.IsSuccess)
            {
                return (false, $"Processing failed: {result.ErrorMessage}");
            }

            // Check for missing variables (warnings)
            if (result.MissingVariables.Any())
            {
                var missing = string.Join(", ", result.MissingVariables);
                return (true, $"Success with warnings. Missing variables: {missing}");
            }

            return (true, $"Success! {result.ReplacementCount} placeholders replaced.");
        }
        catch (IOException ex)
        {
            return (false, $"File error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Unexpected error: {ex.Message}");
        }
    }
}

// Usage
var generator = new DocumentGenerator();
var (success, message) = generator.GenerateDocument(
    "template.docx",
    "output.docx",
    data);

if (success)
{
    Console.WriteLine($"✓ {message}");
}
else
{
    Console.WriteLine($"✗ {message}");
}
```

---

## Processing Multiple Templates

### Batch Processing Example

```csharp
using TriasDev.Templify;

public class BatchProcessor
{
    public async Task ProcessMultipleDocumentsAsync(
        List<(string TemplatePath, string OutputPath, Dictionary<string, object> Data)> documents)
    {
        var processor = new DocumentTemplateProcessor();
        var tasks = new List<Task>();

        foreach (var (templatePath, outputPath, data) in documents)
        {
            tasks.Add(Task.Run(() =>
            {
                using var templateStream = File.OpenRead(templatePath);
                using var outputStream = File.Create(outputPath);

                var result = processor.ProcessTemplate(templateStream, outputStream, data);

                if (result.IsSuccess)
                {
                    Console.WriteLine($"✓ Generated: {outputPath}");
                }
                else
                {
                    Console.WriteLine($"✗ Failed: {outputPath} - {result.ErrorMessage}");
                }
            }));
        }

        await Task.WhenAll(tasks);
    }
}

// Usage
var batch = new BatchProcessor();

var documents = new List<(string, string, Dictionary<string, object>)>
{
    ("invoice-template.docx", "invoice-001.docx", invoiceData1),
    ("invoice-template.docx", "invoice-002.docx", invoiceData2),
    ("invoice-template.docx", "invoice-003.docx", invoiceData3)
};

await batch.ProcessMultipleDocumentsAsync(documents);
```

---

## Web Application Integration

### ASP.NET Core Controller Example

```csharp
using Microsoft.AspNetCore.Mvc;
using TriasDev.Templify;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly DocumentTemplateProcessor _processor;
    private readonly IWebHostEnvironment _environment;

    public DocumentsController(IWebHostEnvironment environment)
    {
        _environment = environment;
        _processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
        });
    }

    [HttpPost("generate-contract")]
    public IActionResult GenerateContract([FromBody] ContractData contractData)
    {
        try
        {
            var templatePath = Path.Combine(_environment.ContentRootPath, "Templates", "contract-template.docx");

            var data = new Dictionary<string, object>
            {
                ["ContractNumber"] = contractData.ContractNumber,
                ["PartyA"] = contractData.PartyA,
                ["PartyB"] = contractData.PartyB,
                ["StartDate"] = contractData.StartDate,
                ["EndDate"] = contractData.EndDate,
                ["Amount"] = contractData.Amount
            };

            using var templateStream = System.IO.File.OpenRead(templatePath);
            using var outputStream = new MemoryStream();

            var result = _processor.ProcessTemplate(templateStream, outputStream, data);

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            // Return file for download
            outputStream.Position = 0;
            var fileName = $"Contract-{contractData.ContractNumber}.docx";

            return File(
                outputStream.ToArray(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class ContractData
{
    public string ContractNumber { get; set; }
    public string PartyA { get; set; }
    public string PartyB { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Amount { get; set; }
}
```

### Dependency Injection Setup

```csharp
// In Program.cs or Startup.cs
services.AddSingleton<DocumentTemplateProcessor>(sp =>
{
    var options = new PlaceholderReplacementOptions
    {
        MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
    };

    return new DocumentTemplateProcessor(options);
});
```

---

## Report Generation

### Monthly Report Example

```csharp
using TriasDev.Templify;

public class MonthlyReportGenerator
{
    private readonly DocumentTemplateProcessor _processor;

    public MonthlyReportGenerator()
    {
        _processor = new DocumentTemplateProcessor();
    }

    public void GenerateMonthlyReport(DateTime reportMonth, List<SalesData> sales)
    {
        // Calculate aggregates
        var totalSales = sales.Sum(s => s.Amount);
        var averageSale = sales.Average(s => s.Amount);
        var topCustomer = sales.GroupBy(s => s.CustomerName)
                              .OrderByDescending(g => g.Sum(s => s.Amount))
                              .First()
                              .Key;

        var data = new Dictionary<string, object>
        {
            ["ReportMonth"] = reportMonth.ToString("MMMM yyyy"),
            ["GeneratedDate"] = DateTime.Now,
            ["TotalSales"] = totalSales,
            ["AverageSale"] = averageSale,
            ["TransactionCount"] = sales.Count,
            ["TopCustomer"] = topCustomer,

            // Department breakdown (example with fixed departments)
            ["DeptAName"] = "Enterprise Sales",
            ["DeptASales"] = sales.Where(s => s.Department == "Enterprise").Sum(s => s.Amount),

            ["DeptBName"] = "SMB Sales",
            ["DeptBSales"] = sales.Where(s => s.Department == "SMB").Sum(s => s.Amount),

            ["DeptCName"] = "Renewals",
            ["DeptCSales"] = sales.Where(s => s.Department == "Renewals").Sum(s => s.Amount)
        };

        var templatePath = "Templates/monthly-report-template.docx";
        var outputPath = $"Reports/Monthly-Report-{reportMonth:yyyy-MM}.docx";

        using var templateStream = File.OpenRead(templatePath);
        using var outputStream = File.Create(outputPath);

        var result = _processor.ProcessTemplate(templateStream, outputStream, data);

        if (result.IsSuccess)
        {
            Console.WriteLine($"Report generated: {outputPath}");
            Console.WriteLine($"Replaced {result.ReplacementCount} placeholders");
        }
        else
        {
            Console.WriteLine($"Report generation failed: {result.ErrorMessage}");
        }
    }
}

public class SalesData
{
    public string CustomerName { get; set; }
    public string Department { get; set; }
    public decimal Amount { get; set; }
}
```

---

## Best Practices

### 1. Reuse Processor Instances

```csharp
// Good - Create once, reuse many times
var processor = new DocumentTemplateProcessor(options);

foreach (var customer in customers)
{
    processor.ProcessTemplate(templateStream, outputStream, GetDataFor(customer));
}

// Avoid - Creating new instance each time (unless options differ)
foreach (var customer in customers)
{
    var processor = new DocumentTemplateProcessor(); // ✗ Unnecessary
    processor.ProcessTemplate(templateStream, outputStream, GetDataFor(customer));
}
```

### 2. Use Meaningful Variable Names

```csharp
// Good - Clear, descriptive names
["InvoiceNumber"] = "INV-2025-001"
["CustomerFullName"] = "John Smith"
["TotalAmountEUR"] = 1299.99m

// Avoid - Ambiguous abbreviations
["InvNo"] = "INV-2025-001"
["CustNm"] = "John Smith"
["Tot"] = 1299.99m
```

### 3. Validate Data Before Processing

```csharp
public ProcessingResult GenerateDocument(Dictionary<string, object> data)
{
    // Validate required fields
    var requiredFields = new[] { "CustomerName", "InvoiceNumber", "TotalAmount" };
    var missingFields = requiredFields.Where(f => !data.ContainsKey(f)).ToList();

    if (missingFields.Any())
    {
        throw new ArgumentException($"Missing required fields: {string.Join(", ", missingFields)}");
    }

    // Proceed with processing
    return _processor.ProcessTemplate(templateStream, outputStream, data);
}
```

### 4. Handle Streams Properly

```csharp
// Good - Using statements ensure disposal
using var templateStream = File.OpenRead(templatePath);
using var outputStream = File.Create(outputPath);

var result = processor.ProcessTemplate(templateStream, outputStream, data);

// Avoid - Manual disposal is error-prone
var templateStream = File.OpenRead(templatePath);
try
{
    var outputStream = File.Create(outputPath);
    try
    {
        processor.ProcessTemplate(templateStream, outputStream, data);
    }
    finally
    {
        outputStream.Dispose();
    }
}
finally
{
    templateStream.Dispose();
}
```

---

## Culture-Specific Formatting Examples

Templify supports culture-specific formatting for numbers, dates, and other locale-sensitive values. This is essential for creating localized documents or ensuring consistent formatting across different systems.

### Example 1: US Invoice with US Formatting

```csharp
using System.Globalization;

var data = new Dictionary<string, object>
{
    ["InvoiceNumber"] = "INV-2025-001",
    ["IssueDate"] = new DateTime(2025, 11, 7, 14, 30, 0),
    ["CustomerName"] = "Acme Corporation",
    ["Items"] = new List<InvoiceItem>
    {
        new InvoiceItem { Description = "Software License", Quantity = 10, UnitPrice = 499.99m, Total = 4999.90m },
        new InvoiceItem { Description = "Annual Support", Quantity = 10, UnitPrice = 99.99m, Total = 999.90m }
    },
    ["Subtotal"] = 5999.80m,
    ["Tax"] = 539.98m,
    ["Total"] = 6539.78m
};

var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("en-US")
};

var processor = new DocumentTemplateProcessor(options);
processor.ProcessTemplate(templateStream, outputStream, data);

// Output formatting:
// Invoice Number: INV-2025-001
// Issue Date: 11/7/2025 2:30:00 PM
// Subtotal: $5999.80
// Tax: $539.98
// Total: $6539.78
```

### Example 2: German Invoice with German Formatting

```csharp
using System.Globalization;

var data = new Dictionary<string, object>
{
    ["RechnungsNummer"] = "RE-2025-001",
    ["Datum"] = new DateTime(2025, 11, 7, 14, 30, 0),
    ["Kunde"] = "BuildCorp GmbH",
    ["Positionen"] = new List<RechnungsPosition>
    {
        new RechnungsPosition { Beschreibung = "Software-Lizenz", Menge = 10, Einzelpreis = 499.99m, Gesamt = 4999.90m },
        new RechnungsPosition { Beschreibung = "Jahres-Support", Menge = 10, Einzelpreis = 99.99m, Gesamt = 999.90m }
    },
    ["Zwischensumme"] = 5999.80m,
    ["MwSt"] = 1139.96m,  // 19% German VAT
    ["Gesamtsumme"] = 7139.76m
};

var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE")
};

var processor = new DocumentTemplateProcessor(options);
processor.ProcessTemplate(templateStream, outputStream, data);

// Output formatting:
// Rechnungsnummer: RE-2025-001
// Datum: 07.11.2025 14:30:00
// Zwischensumme: 5999,80 EUR (note: comma instead of dot)
// MwSt (19%): 1139,96 EUR
// Gesamtsumme: 7139,76 EUR
```

### Example 3: International Report with Invariant Culture

For documents that need consistent formatting regardless of the user's system locale (e.g., scientific reports, international contracts), use `InvariantCulture`:

```csharp
using System.Globalization;

var data = new Dictionary<string, object>
{
    ["ReportTitle"] = "Global Sales Report Q4 2025",
    ["GeneratedDate"] = DateTime.UtcNow,
    ["Regions"] = new List<RegionData>
    {
        new RegionData { Name = "North America", Revenue = 1250000.50m, Growth = 12.5 },
        new RegionData { Name = "Europe", Revenue = 980000.75m, Growth = 8.3 },
        new RegionData { Name = "Asia Pacific", Revenue = 1500000.25m, Growth = 15.7 }
    },
    ["TotalRevenue"] = 3730001.50m,
    ["AverageGrowth"] = 12.17
};

var options = new PlaceholderReplacementOptions
{
    Culture = CultureInfo.InvariantCulture
};

var processor = new DocumentTemplateProcessor(options);
processor.ProcessTemplate(templateStream, outputStream, data);

// Output formatting (consistent across all systems):
// Report Title: Global Sales Report Q4 2025
// Generated: 11/07/2025 14:30:00
// Total Revenue: 3730001.50 (always uses dot)
// Average Growth: 12.17% (always uses dot)
```

### Example 4: Multi-Currency Financial Report

```csharp
using System.Globalization;

// For a report with multiple currencies, use InvariantCulture for numbers
// and handle currency symbols in the template or data
var data = new Dictionary<string, object>
{
    ["ReportDate"] = DateTime.Now,
    ["Accounts"] = new List<Account>
    {
        new Account { Name = "USD Account", Balance = 50000.00m, Currency = "USD" },
        new Account { Name = "EUR Account", Balance = 45000.00m, Currency = "EUR" },
        new Account { Name = "GBP Account", Balance = 38000.00m, Currency = "GBP" }
    },
    ["TotalUSD"] = 133000.00m
};

// Template would have: {{Balance}} {{Currency}}
// Output: 50000.00 USD, 45000.00 EUR, 38000.00 GBP

var options = new PlaceholderReplacementOptions
{
    Culture = CultureInfo.InvariantCulture  // Consistent decimal formatting
};

var processor = new DocumentTemplateProcessor(options);
processor.ProcessTemplate(templateStream, outputStream, data);
```

### Example 5: Date Formatting Across Cultures

```csharp
using System.Globalization;

var eventDate = new DateTime(2025, 11, 7, 18, 30, 0);

// US Format
var dataUS = new Dictionary<string, object>
{
    ["EventName"] = "Annual Conference",
    ["EventDate"] = eventDate
};

var optionsUS = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("en-US")
};
// Output: Event Date: 11/7/2025 6:30:00 PM

// German Format
var dataDE = new Dictionary<string, object>
{
    ["VeranstaltungsName"] = "Jahreskonferenz",
    ["VeranstaltungsDatum"] = eventDate
};

var optionsDE = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE")
};
// Output: Veranstaltungsdatum: 07.11.2025 18:30:00

// UK Format
var dataGB = new Dictionary<string, object>
{
    ["EventName"] = "Annual Conference",
    ["EventDate"] = eventDate
};

var optionsGB = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("en-GB")
};
// Output: Event Date: 07/11/2025 18:30:00
```

### Best Practices for Culture-Specific Documents

1. **Always specify culture explicitly** when creating localized documents:
   ```csharp
   var options = new PlaceholderReplacementOptions
   {
       Culture = new CultureInfo("de-DE")  // Explicit is better than implicit
   };
   ```

2. **Use InvariantCulture for tests** to ensure consistent test results:
   ```csharp
   var options = new PlaceholderReplacementOptions
   {
       Culture = CultureInfo.InvariantCulture
   };
   ```

3. **Match culture to document language**: If your Word template is in German, use German culture for numbers/dates

4. **Consider your audience**:
   - Local invoices → Use local culture (de-DE, en-US, etc.)
   - International reports → Use InvariantCulture
   - Multi-national documents → Consider using multiple templates or custom formatting

5. **Handle currency symbols separately**: Culture only affects number formatting, not currency symbols. Include currency symbols in your template or data.

---

## Troubleshooting Common Issues

### Placeholder Not Replaced

**Problem**: `{{VariableName}}` remains in the output document

**Solutions**:
1. Check variable name spelling (case-sensitive)
2. Ensure variable is in the data dictionary
3. Check MissingVariableBehavior setting
4. Verify placeholder syntax (must be `{{name}}`, no spaces)

### Formatting Lost

**Problem**: Text loses bold/italic formatting after replacement

**Cause**: Current MVP reconstructs runs, may lose complex formatting

**Workaround**: Keep placeholder in a single run (don't split across formatting changes)

### File Locked Error

**Problem**: Cannot write to output file

**Solutions**:
1. Ensure output file is not open in Word
2. Use proper `using` statements for streams
3. Check file permissions

### Table Cells Not Updated

**Problem**: Placeholders in table cells not replaced

**Verify**:
1. Placeholder syntax is correct
2. Table is in document body (not header/footer - not supported in MVP)
3. Check result.MissingVariables for clues

---

For more information, see:
- [README.md](README.md) - Overview and API reference
- [ARCHITECTURE.md](ARCHITECTURE.md) - Design and implementation details
