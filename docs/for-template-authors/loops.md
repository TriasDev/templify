# Loops Guide

Loops let you repeat content for each item in a list. They're essential for creating dynamic documents with variable numbers of items like invoices, reports, and listings.

## Basic Loop Syntax

```
{{#foreach ArrayName}}
  Content to repeat for each item
{{/foreach}}
```

The content between `{{#foreach}}` and `{{/foreach}}` will be repeated once for each item in the array.

## Simple Lists

### Looping Through Text Items

**JSON:**
```json
{
  "Fruits": ["Apple", "Banana", "Cherry", "Date"]
}
```

**Template:**
```
Available Fruits:

{{#foreach Fruits}}
- {{.}}
{{/foreach}}
```

**Output:**
```
Available Fruits:

- Apple
- Banana
- Cherry
- Date
```

**Note:** `{{.}}` (dot) refers to the current item itself when looping through simple values.

### Numbered Lists

**JSON:**
```json
{
  "Steps": [
    "Preheat oven to 350°F",
    "Mix dry ingredients",
    "Add wet ingredients",
    "Bake for 30 minutes"
  ]
}
```

**Template:**
```
Instructions:

{{#foreach Steps}}
{{@index}}. {{.}}
{{/foreach}}
```

**Output:**
```
Instructions:

0. Preheat oven to 350°F
1. Mix dry ingredients
2. Add wet ingredients
3. Bake for 30 minutes
```

**Note:** `{{@index}}` starts at 0. For 1-based numbering, see [Loop Variables](#loop-variables) below.

## Looping Through Objects

### Basic Object Lists

**JSON:**
```json
{
  "Products": [
    {
      "Name": "Widget",
      "Price": 10.00,
      "SKU": "WDG-001"
    },
    {
      "Name": "Gadget",
      "Price": 25.00,
      "SKU": "GDG-002"
    },
    {
      "Name": "Doohickey",
      "Price": 15.00,
      "SKU": "DHK-003"
    }
  ]
}
```

**Template:**
```
Product Catalog:

{{#foreach Products}}
Name: {{Name}}
Price: ${{Price}}
SKU: {{SKU}}
---
{{/foreach}}
```

**Output:**
```
Product Catalog:

Name: Widget
Price: $10.00
SKU: WDG-001
---
Name: Gadget
Price: $25.00
SKU: GDG-002
---
Name: Doohickey
Price: $15.00
SKU: DHK-003
---
```

### Complex Objects

**JSON:**
```json
{
  "Employees": [
    {
      "Name": "Alice Johnson",
      "Title": "Senior Developer",
      "Email": "alice@company.com",
      "Phone": "+1-555-0100"
    },
    {
      "Name": "Bob Smith",
      "Title": "Product Manager",
      "Email": "bob@company.com",
      "Phone": "+1-555-0101"
    }
  ]
}
```

**Template:**
```
EMPLOYEE DIRECTORY

{{#foreach Employees}}
{{Name}} - {{Title}}
  Email: {{Email}}
  Phone: {{Phone}}

{{/foreach}}
```

## Table Loops

One of the most powerful features is repeating table rows:

### Simple Table

**Template (create a table in Word):**

| Product | Price | Stock |
|---------|-------|-------|
| {{#foreach Items}}{{Name}} | ${{Price}} | {{Stock}}{{/foreach}} |

**JSON:**
```json
{
  "Items": [
    { "Name": "Widget", "Price": 10.00, "Stock": 50 },
    { "Name": "Gadget", "Price": 25.00, "Stock": 30 },
    { "Name": "Doohickey", "Price": 15.00, "Stock": 0 }
  ]
}
```

**Result:** The table row will be repeated for each item, creating a table with 4 rows total (1 header + 3 data rows).

### Invoice Line Items

**Template:**

| Item | Quantity | Unit Price | Total |
|------|----------|------------|-------|
| {{#foreach LineItems}}{{Description}} | {{Quantity}} | ${{UnitPrice}} | ${{Total}}{{/foreach}} |

**JSON:**
```json
{
  "LineItems": [
    {
      "Description": "Professional Services",
      "Quantity": 10,
      "UnitPrice": 150.00,
      "Total": 1500.00
    },
    {
      "Description": "Software License",
      "Quantity": 1,
      "UnitPrice": 500.00,
      "Total": 500.00
    },
    {
      "Description": "Support Package",
      "Quantity": 12,
      "UnitPrice": 50.00,
      "Total": 600.00
    }
  ]
}
```

## Loop Variables

Special variables are available inside loops:

### `{{@index}}` - Current Index

Zero-based index of the current item:

**Template:**
```
{{#foreach Items}}
Item #{{@index}}: {{Name}}
{{/foreach}}
```

**Output:**
```
Item #0: Widget
Item #1: Gadget
Item #2: Doohickey
```

**For 1-based numbering, you'll need to adjust in your JSON or just add 1 mentally when reading.**

### `{{@first}}` - First Item

True for the first iteration only:

**JSON:**
```json
{
  "Chapters": [
    { "Title": "Introduction", "Pages": 10 },
    { "Title": "Getting Started", "Pages": 25 },
    { "Title": "Advanced Topics", "Pages": 40 }
  ]
}
```

**Template:**
```
{{#foreach Chapters}}
{{#if @first}}
=== FIRST CHAPTER ===
{{/if}}
Chapter: {{Title}} ({{Pages}} pages)
{{/foreach}}
```

**Output:**
```
=== FIRST CHAPTER ===
Chapter: Introduction (10 pages)
Chapter: Getting Started (25 pages)
Chapter: Advanced Topics (40 pages)
```

### `{{@last}}` - Last Item

True for the last iteration only:

**Template:**
```
{{#foreach Tags}}{{.}}{{#if not @last}}, {{/if}}{{/foreach}}
```

**JSON:**
```json
{
  "Tags": ["JavaScript", "Python", "Java", "C#"]
}
```

**Output:**
```
JavaScript, Python, Java, C#
```

(Notice no comma after the last item!)

### `{{@count}}` - Total Count

Total number of items in the loop:

**Template:**
```
Processing {{@count}} orders:

{{#foreach Orders}}
Order {{@index}} of {{@count}}: {{OrderNumber}}
{{/foreach}}
```

**JSON:**
```json
{
  "Orders": [
    { "OrderNumber": "ORD-001" },
    { "OrderNumber": "ORD-002" },
    { "OrderNumber": "ORD-003" }
  ]
}
```

**Output:**
```
Processing 3 orders:

Order 0 of 3: ORD-001
Order 1 of 3: ORD-002
Order 2 of 3: ORD-003
```

## Nested Loops

You can nest loops within each other for complex data structures:

### Two Levels

**JSON:**
```json
{
  "Departments": [
    {
      "Name": "Engineering",
      "Employees": [
        { "Name": "Alice", "Role": "Senior Dev" },
        { "Name": "Bob", "Role": "Junior Dev" }
      ]
    },
    {
      "Name": "Sales",
      "Employees": [
        { "Name": "Charlie", "Role": "Account Manager" },
        { "Name": "Diana", "Role": "Sales Rep" }
      ]
    }
  ]
}
```

**Template:**
```
{{#foreach Departments}}
Department: {{Name}}
{{#foreach Employees}}
  - {{Name}} ({{Role}})
{{/foreach}}

{{/foreach}}
```

**Output:**
```
Department: Engineering
  - Alice (Senior Dev)
  - Bob (Junior Dev)

Department: Sales
  - Charlie (Account Manager)
  - Diana (Sales Rep)
```

### Three Levels

**JSON:**
```json
{
  "Regions": [
    {
      "Name": "North America",
      "Countries": [
        {
          "Name": "USA",
          "Cities": ["New York", "Los Angeles", "Chicago"]
        },
        {
          "Name": "Canada",
          "Cities": ["Toronto", "Vancouver", "Montreal"]
        }
      ]
    },
    {
      "Name": "Europe",
      "Countries": [
        {
          "Name": "UK",
          "Cities": ["London", "Manchester"]
        }
      ]
    }
  ]
}
```

**Template:**
```
{{#foreach Regions}}
Region: {{Name}}
{{#foreach Countries}}
  Country: {{Name}}
{{#foreach Cities}}
    - {{.}}
{{/foreach}}
{{/foreach}}

{{/foreach}}
```

## Conditionals in Loops

Combine loops with conditionals for powerful templates:

### Conditional Content

**JSON:**
```json
{
  "Products": [
    { "Name": "Widget", "Price": 10, "InStock": true },
    { "Name": "Gadget", "Price": 25, "InStock": false },
    { "Name": "Doohickey", "Price": 15, "InStock": true }
  ]
}
```

**Template:**
```
Product List:

{{#foreach Products}}
{{Name}} - ${{Price}}
{{#if InStock}}
  ✓ Available now
{{else}}
  ❌ Out of stock
{{/if}}

{{/foreach}}
```

### Filtering with Conditionals

**JSON:**
```json
{
  "Orders": [
    { "Id": "001", "Status": "Completed", "Total": 100 },
    { "Id": "002", "Status": "Pending", "Total": 50 },
    { "Id": "003", "Status": "Completed", "Total": 200 },
    { "Id": "004", "Status": "Cancelled", "Total": 75 }
  ]
}
```

**Template:**
```
Completed Orders:

{{#foreach Orders}}
{{#if Status = "Completed"}}
Order {{Id}}: ${{Total}}
{{/if}}
{{/foreach}}
```

**Output:**
```
Completed Orders:

Order 001: $100
Order 003: $200
```

## Variable Resolution in Nested Loops

When you use a variable inside a loop, Templify searches for it in a specific order. Understanding this resolution order is important for working with nested loops.

### Resolution Order

Variables are resolved from **innermost scope to outermost scope** (first match wins):

```
1. Current loop item (innermost)
2. Parent loop item
3. Grandparent loop item
4. ... (any additional parent loops)
5. Global context (outermost)
```

### Basic Example

**JSON:**
```json
{
  "CompanyName": "Acme Corp",
  "Departments": [
    {
      "DepartmentName": "Engineering",
      "Employees": [
        { "EmployeeName": "Alice" },
        { "EmployeeName": "Bob" }
      ]
    }
  ]
}
```

**Template:**
```
{{#foreach Departments}}
{{CompanyName}} - {{DepartmentName}}
{{#foreach Employees}}
  Employee: {{EmployeeName}}
  Department: {{DepartmentName}}
  Company: {{CompanyName}}
{{/foreach}}
{{/foreach}}
```

**How resolution works inside the inner loop:**
- `{{EmployeeName}}` → Found on current loop item (Employee)
- `{{DepartmentName}}` → Not on Employee → Found on parent loop item (Department)
- `{{CompanyName}}` → Not on Employee → Not on Department → Found in global context

### Variable Shadowing

When both inner and outer contexts have a property with the **same name**, the inner one always wins. This is called "shadowing".

**JSON:**
```json
{
  "Categories": [
    {
      "Name": "Electronics",
      "Items": [
        { "Name": "Laptop" },
        { "Name": "Mouse" }
      ]
    }
  ]
}
```

**Template:**
```
{{#foreach Categories}}
Category: {{Name}}           ← Shows "Electronics"
{{#foreach Items}}
  Item: {{Name}}             ← Shows "Laptop" / "Mouse" (inner shadows outer)
{{/foreach}}
{{/foreach}}
```

**Output:**
```
Category: Electronics
  Item: Laptop
  Item: Mouse
```

⚠️ **Important:** Inside the inner loop, there is **no way** to access the outer `Name` because it's shadowed by the inner `Name`.

### Best Practice: Use Unique Property Names

To avoid shadowing issues, use **unique property names** at each level:

**❌ Problematic (same name at multiple levels):**
```json
{
  "Categories": [
    {
      "Name": "Electronics",
      "Items": [
        { "Name": "Laptop" }
      ]
    }
  ]
}
```

**✅ Better (unique names):**
```json
{
  "Categories": [
    {
      "CategoryName": "Electronics",
      "Items": [
        { "ItemName": "Laptop" }
      ]
    }
  ]
}
```

**Template:**
```
{{#foreach Categories}}
Category: {{CategoryName}}
{{#foreach Items}}
  Item: {{ItemName}}
  Category: {{CategoryName}}    ← Now accessible!
{{/foreach}}
{{/foreach}}
```

### Accessing Global Variables in Nested Loops

Global variables remain accessible at any nesting depth (unless shadowed):

**JSON:**
```json
{
  "ShowPrices": true,
  "Currency": "USD",
  "Orders": [
    {
      "OrderId": "ORD-001",
      "Items": [
        { "ProductName": "Widget", "Price": 10 }
      ]
    }
  ]
}
```

**Template:**
```
{{#foreach Orders}}
Order: {{OrderId}}
{{#foreach Items}}
  {{#if ShowPrices}}
    {{ProductName}}: {{Price}} {{Currency}}
  {{else}}
    {{ProductName}}: Contact for pricing
  {{/if}}
{{/foreach}}
{{/foreach}}
```

Here, `{{ShowPrices}}` and `{{Currency}}` are resolved from the global context because they're not defined on the loop items.

### Using Conditionals with Parent Data

You can use conditionals that reference parent loop properties:

**JSON:**
```json
{
  "Categories": [
    {
      "CategoryName": "Premium",
      "IsPremium": true,
      "Products": [
        { "ProductName": "Gold Widget" },
        { "ProductName": "Gold Gadget" }
      ]
    },
    {
      "CategoryName": "Standard",
      "IsPremium": false,
      "Products": [
        { "ProductName": "Basic Widget" }
      ]
    }
  ]
}
```

**Template:**
```
{{#foreach Categories}}
{{CategoryName}} Products:
{{#foreach Products}}
  {{#if IsPremium}}★ {{/if}}{{ProductName}}
{{/foreach}}

{{/foreach}}
```

**Output:**
```
Premium Products:
  ★ Gold Widget
  ★ Gold Gadget

Standard Products:
  Basic Widget
```

The `{{#if IsPremium}}` condition checks the parent category's property from within the inner Products loop.

### Summary Table

| Scenario | Resolution |
|----------|------------|
| Property only on current item | ✅ Found immediately |
| Property only on parent item | ✅ Found after checking current |
| Property only in global context | ✅ Found after checking all loop levels |
| Same property name at multiple levels | ⚠️ Innermost wins (shadowing) |
| Need to access shadowed property | ❌ Not possible - use unique names |

## Empty Arrays

What happens when an array is empty?

**JSON:**
```json
{
  "Items": []
}
```

**Template:**
```
Items:
{{#foreach Items}}
- {{Name}}
{{/foreach}}
(End of list)
```

**Output:**
```
Items:
(End of list)
```

The loop body simply doesn't execute when the array is empty.

### Handling Empty Arrays

**Template:**
```
{{#if Items}}
Items:
{{#foreach Items}}
- {{Name}}
{{/foreach}}
{{else}}
No items available.
{{/if}}
```

## Common Patterns

### Comma-Separated List

**JSON:**
```json
{
  "Authors": ["Alice Johnson", "Bob Smith", "Charlie Brown"]
}
```

**Template:**
```
Authors: {{#foreach Authors}}{{.}}{{#if not @last}}, {{/if}}{{/foreach}}
```

**Output:**
```
Authors: Alice Johnson, Bob Smith, Charlie Brown
```

### Bulleted List

**Template:**
```
Key Features:
{{#foreach Features}}
• {{.}}
{{/foreach}}
```

### Numbered List (1-based)

Since `{{@index}}` is zero-based, here's a workaround for 1-based numbering:

**JSON:**
```json
{
  "Tasks": [
    { "Number": 1, "Task": "First task" },
    { "Number": 2, "Task": "Second task" },
    { "Number": 3, "Task": "Third task" }
  ]
}
```

**Template:**
```
{{#foreach Tasks}}
{{Number}}. {{Task}}
{{/foreach}}
```

Or include a calculated number in your JSON data.

### Alternating Rows

**JSON:**
```json
{
  "Items": [
    { "Name": "Item 1" },
    { "Name": "Item 2" },
    { "Name": "Item 3" },
    { "Name": "Item 4" }
  ]
}
```

**Template (in Word with background color):**
```
{{#foreach Items}}
{{@index}}: {{Name}}
{{/foreach}}
```

Then manually apply alternating row colors in Word, or use conditional formatting based on `{{@index}}` if you process the index modulo 2 in your JSON.

### Section Separators

**Template:**
```
{{#foreach Sections}}
{{Title}}

{{Content}}

{{#if not @last}}
─────────────────
{{/if}}
{{/foreach}}
```

This adds a separator between sections but not after the last one.

## Real-World Examples

### Invoice

**JSON:**
```json
{
  "InvoiceNumber": "INV-2024-001",
  "InvoiceDate": "2024-01-15",
  "Customer": {
    "Name": "Acme Corporation",
    "Address": "123 Business St, City, State 12345"
  },
  "LineItems": [
    {
      "Description": "Website Design",
      "Quantity": 1,
      "Rate": 5000.00,
      "Amount": 5000.00
    },
    {
      "Description": "Hosting (12 months)",
      "Quantity": 12,
      "Rate": 50.00,
      "Amount": 600.00
    },
    {
      "Description": "Domain Registration",
      "Quantity": 1,
      "Rate": 15.00,
      "Amount": 15.00
    }
  ],
  "Subtotal": 5615.00,
  "Tax": 449.20,
  "Total": 6064.20
}
```

**Template:**
```
INVOICE #{{InvoiceNumber}}
Date: {{InvoiceDate}}

BILL TO:
{{Customer.Name}}
{{Customer.Address}}

ITEMS:
| Description | Qty | Rate | Amount |
|-------------|-----|------|--------|
{{#foreach LineItems}}
| {{Description}} | {{Quantity}} | ${{Rate}} | ${{Amount}} |
{{/foreach}}

Subtotal: ${{Subtotal}}
Tax: ${{Tax}}
TOTAL: ${{Total}}
```

### Meeting Attendees

**JSON:**
```json
{
  "MeetingTitle": "Q1 Planning Session",
  "MeetingDate": "2024-01-20",
  "Attendees": [
    {
      "Name": "Alice Johnson",
      "Department": "Engineering",
      "Role": "Required"
    },
    {
      "Name": "Bob Smith",
      "Department": "Product",
      "Role": "Required"
    },
    {
      "Name": "Charlie Brown",
      "Department": "Design",
      "Role": "Optional"
    }
  ]
}
```

**Template:**
```
Meeting: {{MeetingTitle}}
Date: {{MeetingDate}}

ATTENDEES:

Required:
{{#foreach Attendees}}
{{#if Role = "Required"}}
- {{Name}} ({{Department}})
{{/if}}
{{/foreach}}

Optional:
{{#foreach Attendees}}
{{#if Role = "Optional"}}
- {{Name}} ({{Department}})
{{/if}}
{{/foreach}}
```

### Product Catalog with Categories

**JSON:**
```json
{
  "Categories": [
    {
      "Name": "Electronics",
      "Products": [
        { "Name": "Laptop", "Price": 999 },
        { "Name": "Mouse", "Price": 25 }
      ]
    },
    {
      "Name": "Books",
      "Products": [
        { "Name": "Learn Python", "Price": 40 },
        { "Name": "Web Design", "Price": 35 }
      ]
    }
  ]
}
```

**Template:**
```
PRODUCT CATALOG

{{#foreach Categories}}
━━━━━━━━━━━━━━━━━━━━
{{Name}}
━━━━━━━━━━━━━━━━━━━━

{{#foreach Products}}
• {{Name}} - ${{Price}}
{{/foreach}}

{{/foreach}}
```

## Troubleshooting

### Loop Not Repeating

**Check:**
1. JSON has an array: `"Items": [...]` not `"Items": {...}`
2. Array name matches exactly (case-sensitive)
3. Closing tag is present: `{{/foreach}}`

### Wrong Data Appears

**Check:**
1. Property names inside loop match the object structure
2. Not confusing parent and nested properties

**JSON:**
```json
{
  "Products": [
    { "ProductName": "Widget" }
  ]
}
```

**Template:**
```
{{#foreach Products}}
{{Name}}         ← Wrong! Should be {{ProductName}}
{{ProductName}}  ← Correct!
{{/foreach}}
```

### Nested Loops Not Working

Make sure closing tags are in the right order:

**❌ Wrong:**
```
{{#foreach A}}
  {{#foreach B}}
  {{/foreach}}
{{/foreach}}     ← Closed A, should close B first
```

**✅ Correct:**
```
{{#foreach A}}
  {{#foreach B}}
  {{/foreach}}   ← Close B
{{/foreach}}     ← Close A
```

## Best Practices

1. **Structure JSON to match template needs** - Organize data how you'll display it
2. **Use meaningful property names** - `ProductName` not `N1`
3. **Use unique property names in nested structures** - Avoid `Name` at multiple levels; use `CategoryName`, `ItemName` instead (see [Variable Shadowing](#variable-shadowing))
4. **Keep nesting reasonable** - 2-3 levels maximum for readability
5. **Test with edge cases** - Empty arrays, single items, many items
6. **Use loop variables** - `{{@first}}`, `{{@last}}` for special formatting
7. **Combine with conditionals** - Filter or format items based on properties
8. **Add separators carefully** - Use `{{#if not @last}}` to avoid trailing separators

## Next Steps

- **[Conditionals Guide](conditionals.md)** - Combining loops with if/else logic
- **[Placeholders Guide](placeholders.md)** - Accessing nested properties in loops
- **[Template Syntax Reference](template-syntax.md)** - Complete syntax guide
- **[Examples Gallery](examples-gallery.md)** - Real-world loop examples

## Related Topics

- [Format Specifiers](format-specifiers.md) - Format values inside loops
- [Best Practices](best-practices.md) - Tips for maintainable templates
- [JSON Basics](json-basics.md) - Understanding arrays and objects
