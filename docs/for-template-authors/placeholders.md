# Placeholders Guide

Placeholders are the foundation of Templify templates. They mark where data from your JSON file should be inserted into the document.

## Basic Placeholder Syntax

A placeholder consists of:
1. Opening double curly braces: `{{`
2. The variable name
3. Closing double curly braces: `}}`

```
{{VariableName}}
```

**Important rules:**
- Use exactly **two** curly braces on each side
- No spaces inside the braces: `{{Name}}` not `{{ Name }}`
- Names are **case-sensitive**: `{{Name}}` ≠ `{{name}}`

## Simple Placeholders

### Basic Example

**JSON (data.json):**
```json
{
  "CompanyName": "Acme Corporation",
  "Year": 2024,
  "IsActive": true
}
```

**Template:**
```
Company: {{CompanyName}}
Year: {{Year}}
Active: {{IsActive}}
```

**Output:**
```
Company: Acme Corporation
Year: 2024
Active: true
```

### Text Replacement

Any text value from JSON is inserted as-is:

**JSON:**
```json
{
  "CustomerName": "Alice Johnson",
  "Email": "alice@example.com",
  "PhoneNumber": "+1-555-0123"
}
```

**Template:**
```
Customer: {{CustomerName}}
Contact: {{Email}} or {{PhoneNumber}}
```

### Numbers

Numbers are converted to text automatically:

**JSON:**
```json
{
  "Quantity": 5,
  "Price": 19.99,
  "Discount": 0.15,
  "Total": 84.96
}
```

**Template:**
```
Quantity: {{Quantity}}
Price: ${{Price}}
Discount: {{Discount}}
Total: ${{Total}}
```

**Output:**
```
Quantity: 5
Price: $19.99
Discount: 0.15
Total: $84.96
```

### Boolean Values

True/false values are shown as "true" or "false":

**JSON:**
```json
{
  "IsVIP": true,
  "HasDiscount": false
}
```

**Template:**
```
VIP Status: {{IsVIP}}
Has Discount: {{HasDiscount}}
```

**Output:**
```
VIP Status: true
Has Discount: false
```

**Tip:** Use format specifiers for better display (see [Format Specifiers](format-specifiers.md)):
- `{{IsVIP:yesno}}` → "Yes"
- `{{HasDiscount:checkbox}}` → "☐"

## Nested Properties

Use dot notation (`.`) to access nested data structures:

### Two Levels Deep

**JSON:**
```json
{
  "Customer": {
    "Name": "Bob Smith",
    "Email": "bob@example.com"
  }
}
```

**Template:**
```
Customer Name: {{Customer.Name}}
Email: {{Customer.Email}}
```

### Multiple Levels Deep

**JSON:**
```json
{
  "Company": {
    "Name": "Acme Corp",
    "Address": {
      "Street": "123 Main St",
      "City": "Springfield",
      "State": "IL",
      "PostalCode": {
        "Zip": "62701",
        "Plus4": "1234"
      }
    }
  }
}
```

**Template:**
```
Company: {{Company.Name}}
Address: {{Company.Address.Street}}
         {{Company.Address.City}}, {{Company.Address.State}} {{Company.Address.PostalCode.Zip}}
```

### Complex Nested Structure

**JSON:**
```json
{
  "Order": {
    "Id": "ORD-12345",
    "Customer": {
      "Name": "Sarah Connor",
      "Contact": {
        "Email": "sarah@example.com",
        "Phone": {
          "Mobile": "+1-555-0199",
          "Home": "+1-555-0188"
        }
      }
    },
    "ShippingAddress": {
      "Street": "456 Oak Ave",
      "City": "Los Angeles"
    }
  }
}
```

**Template:**
```
Order #{{Order.Id}} for {{Order.Customer.Name}}

Contact: {{Order.Customer.Contact.Email}}
Mobile: {{Order.Customer.Contact.Phone.Mobile}}

Ship to: {{Order.ShippingAddress.Street}}, {{Order.ShippingAddress.City}}
```

## Array Access

### Accessing Array Items by Index

Arrays use zero-based indexing (`[0]` is the first item):

**JSON:**
```json
{
  "Colors": ["Red", "Green", "Blue", "Yellow"]
}
```

**Template:**
```
First color: {{Colors[0]}}
Second color: {{Colors[1]}}
Fourth color: {{Colors[3]}}
```

**Output:**
```
First color: Red
Second color: Green
Fourth color: Yellow
```

### Array of Objects

Access properties of array items:

**JSON:**
```json
{
  "Employees": [
    {
      "Name": "Alice Johnson",
      "Title": "Manager",
      "Email": "alice@company.com"
    },
    {
      "Name": "Bob Smith",
      "Title": "Developer",
      "Email": "bob@company.com"
    }
  ]
}
```

**Template:**
```
Manager: {{Employees[0].Name}} ({{Employees[0].Email}})
Developer: {{Employees[1].Name}} ({{Employees[1].Email}})
```

### Nested Arrays

**JSON:**
```json
{
  "Departments": [
    {
      "Name": "Sales",
      "Teams": [
        { "Name": "East Coast", "Size": 5 },
        { "Name": "West Coast", "Size": 7 }
      ]
    }
  ]
}
```

**Template:**
```
Department: {{Departments[0].Name}}
First Team: {{Departments[0].Teams[0].Name}} ({{Departments[0].Teams[0].Size}} members)
Second Team: {{Departments[0].Teams[1].Name}} ({{Departments[0].Teams[1].Size}} members)
```

## Dictionary/Map Access

Access dictionary values using bracket notation with keys:

**JSON:**
```json
{
  "Settings": {
    "Theme": "Dark",
    "Language": "English",
    "Timezone": "UTC-5"
  }
}
```

**Template (two ways to access):**
```
Theme: {{Settings.Theme}}
Language: {{Settings.Language}}
Timezone: {{Settings.Timezone}}
```

Or with brackets (useful for keys with special characters):
```
Theme: {{Settings[Theme]}}
Language: {{Settings[Language]}}
```

## Combining Techniques

You can combine nested properties and array indexing:

**JSON:**
```json
{
  "Company": {
    "Departments": [
      {
        "Name": "Engineering",
        "Manager": {
          "Name": "Dr. Sarah Chen",
          "Email": "sarah.chen@company.com",
          "ContactNumbers": [
            "+1-555-0100",
            "+1-555-0101"
          ]
        }
      },
      {
        "Name": "Sales",
        "Manager": {
          "Name": "Mike Rodriguez",
          "Email": "mike.r@company.com",
          "ContactNumbers": [
            "+1-555-0200"
          ]
        }
      }
    ]
  }
}
```

**Template:**
```
Engineering Manager: {{Company.Departments[0].Manager.Name}}
Email: {{Company.Departments[0].Manager.Email}}
Primary Phone: {{Company.Departments[0].Manager.ContactNumbers[0]}}
Secondary Phone: {{Company.Departments[0].Manager.ContactNumbers[1]}}

Sales Manager: {{Company.Departments[1].Manager.Name}}
Email: {{Company.Departments[1].Manager.Email}}
Phone: {{Company.Departments[1].Manager.ContactNumbers[0]}}
```

## Special Considerations

### Missing Data

If a placeholder refers to data that doesn't exist in your JSON:

**JSON:**
```json
{
  "FirstName": "Alice"
}
```

**Template:**
```
Name: {{FirstName}} {{LastName}}
```

**Output (default behavior):**
```
Name: Alice {{LastName}}
```

The placeholder remains unchanged if the data is missing. This helps you spot missing data easily.

### Null Values

If a value is explicitly null in JSON:

**JSON:**
```json
{
  "Name": "Alice",
  "MiddleName": null
}
```

**Template:**
```
Full Name: {{Name}} {{MiddleName}}
```

The null value is treated as empty text.

### Empty Strings

**JSON:**
```json
{
  "Name": "Alice",
  "MiddleName": ""
}
```

Empty strings are replaced with nothing (empty text).

### Case Sensitivity

Placeholder name matching depends on your data structure:

**Dictionary keys (JSON) are case-sensitive:**

**JSON:**
```json
{
  "customerName": "Alice",
  "CustomerName": "Bob"
}
```

**Template:**
```
{{customerName}}  → Alice
{{CustomerName}}  → Bob
{{customername}}  → {{customername}} (not found!)
```

**Note for developers:** If your data comes from code (not JSON files), property names may be case-insensitive depending on how the data is structured.

**Best practice for template authors:** Always match the exact case used in your JSON keys to avoid confusion and ensure templates work reliably.

## Formatting Placeholders

You can apply formatting to placeholders using format specifiers:

### Basic Syntax

```
{{VariableName:FormatSpecifier}}
```

### Common Examples

**JSON:**
```json
{
  "Name": "alice johnson",
  "Price": 1234.567,
  "IsActive": true,
  "OrderDate": "2024-01-15"
}
```

**Template:**
```
Name: {{Name:uppercase}}
Price: {{Price:currency}}
Active: {{IsActive:yesno}}
Date: {{OrderDate:date:MMMM d, yyyy}}
```

**Output:**
```
Name: ALICE JOHNSON
Price: $1,234.57
Active: Yes
Date: January 15, 2024
```

For complete formatting options, see [Format Specifiers Guide](format-specifiers.md).

## Markdown Formatting in Data

You can include markdown formatting in your JSON data values:

**JSON:**
```json
{
  "Message": "Hello **Alice**, welcome to *our platform*!",
  "Warning": "This is ~~old~~ information.",
  "Emphasis": "This is ***very important***!"
}
```

**Template:**
```
{{Message}}
{{Warning}}
{{Emphasis}}
```

**Output (with formatting applied):**
```
Hello Alice, welcome to our platform!
This is old information.
This is very important!
```

The markdown syntax (`**bold**`, `*italic*`, `~~strikethrough~~`) is converted to actual formatting in the Word document.

## Line Breaks in Data Values

Newline characters in your data values are automatically converted to line breaks in Word:

**JSON:**
```json
{
  "Address": "123 Main Street\nApartment 4B\nNew York, NY 10001",
  "Note": "First line.\r\nSecond line."
}
```

**Template:**
```
Delivery Address:
{{Address}}

Note: {{Note}}
```

**Output:**
```
Delivery Address:
123 Main Street
Apartment 4B
New York, NY 10001

Note: First line.
Second line.
```

All newline formats are supported: `\n` (Unix/Linux/macOS), `\r\n` (Windows), `\r` (legacy Mac).

**Combining with Markdown:**

You can use both markdown formatting and line breaks together:

```json
{
  "Instructions": "**Step 1:** Open the file\n**Step 2:** Edit the content\n**Step 3:** Save and close"
}
```

Each line will be formatted with bold text and separated by line breaks.

## Whitespace Handling

### Spaces Around Placeholders

Spaces around placeholders are preserved:

**Template:**
```
Hello {{Name}} , welcome!
```

**Output:**
```
Hello Alice , welcome!
```

Note the space before the comma. Be mindful of spacing!

**Better:**
```
Hello {{Name}}, welcome!
```

### Line Breaks

Placeholders can appear anywhere in your text:

**Template:**
```
Hello {{Name}},

Thank you for your order #{{OrderNumber}}.

We'll ship to:
{{Address.Street}}
{{Address.City}}, {{Address.State}} {{Address.Zip}}
```

All line breaks and formatting are preserved.

## Best Practices

### 1. Use Descriptive Names

**❌ Bad:**
```json
{
  "n": "Alice",
  "e": "alice@example.com",
  "p": "+1-555-0123"
}
```

**✅ Good:**
```json
{
  "CustomerName": "Alice",
  "Email": "alice@example.com",
  "PhoneNumber": "+1-555-0123"
}
```

### 2. Group Related Data

**❌ Flat:**
```json
{
  "CustomerName": "Alice",
  "CustomerEmail": "alice@example.com",
  "CustomerCity": "New York",
  "CustomerState": "NY"
}
```

**✅ Nested:**
```json
{
  "Customer": {
    "Name": "Alice",
    "Email": "alice@example.com",
    "Address": {
      "City": "New York",
      "State": "NY"
    }
  }
}
```

### 3. Match JSON Structure to Template Logic

Structure your JSON to match how you'll use it in templates:

**Template:**
```
Bill To: {{BillingAddress.Name}}
         {{BillingAddress.Street}}
         {{BillingAddress.City}}

Ship To: {{ShippingAddress.Name}}
         {{ShippingAddress.Street}}
         {{ShippingAddress.City}}
```

**JSON:**
```json
{
  "BillingAddress": {
    "Name": "Alice Johnson",
    "Street": "123 Main St",
    "City": "New York"
  },
  "ShippingAddress": {
    "Name": "Bob Smith",
    "Street": "456 Oak Ave",
    "City": "Los Angeles"
  }
}
```

### 4. Test with Sample Data First

Start with simple test data to verify your placeholders work:

**Test JSON:**
```json
{
  "Name": "TEST",
  "Email": "TEST@EMAIL.COM"
}
```

This makes it obvious if placeholders are working.

### 5. Use Format Specifiers for Better Output

Instead of raw boolean values:
```
Status: {{IsActive}}  → Status: true
```

Use format specifiers:
```
Status: {{IsActive:yesno}}  → Status: Yes
```

## Troubleshooting

### Placeholder Not Replaced

**Check:**
1. Exact spelling (case-sensitive): `{{Name}}` vs `{{name}}`
2. Proper syntax: `{{Name}}` not `{Name}` or `{{ Name }}`
3. JSON has the key: `"Name": "..."`
4. JSON is valid (use jsonlint.com)

### Wrong Value Appears

**Check:**
1. JSON path is correct: `{{Customer.Name}}` needs `{ "Customer": { "Name": "..." } }`
2. Array index is correct: `{{Items[0]}}` (zero-based)
3. No duplicate keys in JSON

### Formatting Not Applied

**Check:**
1. Format specifier syntax: `{{Value:format}}` not `{{Value format}}`
2. Format specifier name is correct (see [Format Specifiers](format-specifiers.md))

## Real-World Examples

### Invoice Header

**JSON:**
```json
{
  "Invoice": {
    "Number": "INV-2024-001",
    "Date": "2024-01-15",
    "DueDate": "2024-02-15"
  },
  "Customer": {
    "Name": "Acme Corporation",
    "Contact": "John Doe",
    "Email": "john@acme.com",
    "Address": {
      "Street": "789 Business Blvd",
      "City": "Chicago",
      "State": "IL",
      "Zip": "60601"
    }
  }
}
```

**Template:**
```
INVOICE #{{Invoice.Number}}
Date: {{Invoice.Date}}
Due Date: {{Invoice.DueDate}}

BILL TO:
{{Customer.Name}}
Attn: {{Customer.Contact}}
{{Customer.Address.Street}}
{{Customer.Address.City}}, {{Customer.Address.State}} {{Customer.Address.Zip}}

Contact: {{Customer.Email}}
```

### Certificate Template

**JSON:**
```json
{
  "Recipient": {
    "Name": "Jane Smith",
    "Title": "Senior Developer"
  },
  "Course": {
    "Name": "Advanced Software Architecture",
    "CompletionDate": "2024-01-20",
    "Score": 95,
    "Hours": 40
  },
  "Instructor": {
    "Name": "Dr. Robert Johnson",
    "Credentials": "PhD, Senior Architect"
  }
}
```

**Template:**
```
CERTIFICATE OF COMPLETION

This certifies that

{{Recipient.Name}}
{{Recipient.Title}}

has successfully completed

{{Course.Name}}

Date: {{Course.CompletionDate}}
Score: {{Course.Score}}%
Hours: {{Course.Hours}}

Instructor: {{Instructor.Name}}, {{Instructor.Credentials}}
```

## Next Steps

- **[Conditionals Guide](conditionals.md)** - Show/hide content based on data
- **[Loops Guide](loops.md)** - Repeat content for arrays
- **[Format Specifiers](format-specifiers.md)** - Format numbers, dates, and more
- **[Template Syntax Reference](template-syntax.md)** - Complete syntax guide
- **[Examples Gallery](examples-gallery.md)** - Real-world templates

## Related Topics

- [JSON Basics](json-basics.md) - Understanding JSON structure
- [Boolean Expressions](boolean-expressions.md) - Using placeholders in conditions
- [Best Practices](best-practices.md) - Tips for effective templates
