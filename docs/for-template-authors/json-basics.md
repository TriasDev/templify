# JSON Basics for Template Authors

JSON (JavaScript Object Notation) is a simple way to store and organize data in a text file. Don't let the technical-sounding name intimidate you - it's actually quite straightforward once you understand the basics!

## Why JSON?

Templify uses JSON to provide the data that fills in your template placeholders. Think of JSON as a way to write down information in a structured format that computers can easily read.

## The Five Types of Data in JSON

### 1. Text (Strings)

Text values must be wrapped in double quotes:

```json
{
  "Name": "Alice Johnson",
  "Email": "alice@example.com",
  "Message": "Hello, world!"
}
```

**Rules for text:**
- Always use **double quotes** (`"`) not single quotes (`'`)
- To include a quote inside text, use `\"`: `"She said \"Hello\""`
- To include a backslash, use `\\`: `"C:\\Users\\Documents"`

### 2. Numbers

Numbers don't need quotes:

```json
{
  "Age": 25,
  "Price": 19.99,
  "Quantity": 100,
  "Temperature": -5.5
}
```

**Rules for numbers:**
- No quotes around numbers
- Use a dot (`.`) for decimals, not a comma
- Negative numbers start with `-`

### 3. True/False (Booleans)

For yes/no values, use `true` or `false` (no quotes, all lowercase):

```json
{
  "IsActive": true,
  "IsPremium": false,
  "HasDiscount": true
}
```

**Rules for true/false:**
- Must be lowercase: `true` or `false`
- No quotes around them
- These are perfect for conditionals in your templates

### 4. Lists (Arrays)

Lists let you have multiple values. They're wrapped in square brackets `[ ]`:

```json
{
  "Colors": ["Red", "Green", "Blue"],
  "Prices": [10.99, 25.00, 15.50],
  "Tags": ["new", "featured", "sale"]
}
```

**Rules for lists:**
- Wrap the list in square brackets: `[ ]`
- Separate items with commas
- All items should be the same type (all text, all numbers, etc.)
- Can be empty: `[]`

### 5. Objects (Nested Data)

Objects let you group related data together. They're wrapped in curly braces `{ }`:

```json
{
  "Customer": {
    "Name": "Bob Smith",
    "Age": 30,
    "Email": "bob@example.com"
  }
}
```

**Rules for objects:**
- Wrap in curly braces: `{ }`
- Each piece of data is `"key": value`
- Separate pieces with commas
- Can contain any type of data, including other objects and lists

## Complete JSON File Structure

Every JSON file for Templify follows this pattern:

```json
{
  "Field1": "value",
  "Field2": "value",
  "Field3": "value"
}
```

**Key rules:**
1. **Start with `{` and end with `}`** - These wrap everything
2. **Each line has `"Name": value`** - The name (key) and its value
3. **Separate lines with commas** - But NOT after the last line
4. **Names (keys) must have double quotes** - Values depend on the type

## Common Examples

### Simple Contact Information

```json
{
  "FirstName": "Sarah",
  "LastName": "Connor",
  "Phone": "+1-555-0123",
  "Age": 28,
  "IsSubscribed": true
}
```

### Nested Information (Objects Within Objects)

```json
{
  "Company": {
    "Name": "Acme Corp",
    "Founded": 1995,
    "Address": {
      "Street": "123 Main St",
      "City": "Springfield",
      "Country": "USA"
    }
  }
}
```

**In your template, access nested data with dots:**
- `{{Company.Name}}` → "Acme Corp"
- `{{Company.Address.City}}` → "Springfield"

### Lists of Items

```json
{
  "CustomerName": "John Doe",
  "OrderItems": [
    {
      "ProductName": "Widget",
      "Quantity": 2,
      "Price": 10.00
    },
    {
      "ProductName": "Gadget",
      "Quantity": 1,
      "Price": 25.00
    }
  ]
}
```

**In your template, use loops:**

```
{{#foreach OrderItems}}
- {{ProductName}}: ${{Price}} (Qty: {{Quantity}})
{{/foreach}}
```

### Combining Everything

```json
{
  "CustomerName": "Alice Johnson",
  "IsVIP": true,
  "TotalSpent": 1250.50,
  "RecentOrders": [
    {
      "OrderDate": "2024-01-15",
      "Amount": 50.00,
      "Status": "Delivered"
    },
    {
      "OrderDate": "2024-02-20",
      "Amount": 75.00,
      "Status": "Shipped"
    }
  ],
  "PreferredContact": {
    "Method": "Email",
    "Value": "alice@example.com",
    "SendPromotions": true
  }
}
```

## Common Mistakes and How to Fix Them

### ❌ Missing Comma

**Wrong:**
```json
{
  "Name": "Alice"
  "Age": 25
}
```

**Right:**
```json
{
  "Name": "Alice",
  "Age": 25
}
```

### ❌ Extra Comma at the End

**Wrong:**
```json
{
  "Name": "Alice",
  "Age": 25,
}
```

**Right:**
```json
{
  "Name": "Alice",
  "Age": 25
}
```

### ❌ Missing Quotes Around Keys

**Wrong:**
```json
{
  Name: "Alice",
  Age: 25
}
```

**Right:**
```json
{
  "Name": "Alice",
  "Age": 25
}
```

### ❌ Single Quotes Instead of Double Quotes

**Wrong:**
```json
{
  'Name': 'Alice',
  'Age': 25
}
```

**Right:**
```json
{
  "Name": "Alice",
  "Age": 25
}
```

### ❌ Numbers in Quotes

If you put numbers in quotes, they become text (usually fine, but can cause issues with comparisons):

**Less than ideal:**
```json
{
  "Age": "25",
  "Price": "19.99"
}
```

**Better:**
```json
{
  "Age": 25,
  "Price": 19.99
}
```

### ❌ Missing Closing Bracket or Brace

**Wrong:**
```json
{
  "Items": ["Apple", "Banana", "Cherry"
}
```

**Right:**
```json
{
  "Items": ["Apple", "Banana", "Cherry"]
}
```

## Practical Tips

### Start Small

Begin with simple data and gradually add complexity:

**Step 1: Simple**
```json
{
  "Name": "Test"
}
```

**Step 2: Add More**
```json
{
  "Name": "Test",
  "Email": "test@example.com"
}
```

**Step 3: Add Nesting**
```json
{
  "Name": "Test",
  "Email": "test@example.com",
  "Address": {
    "City": "Springfield"
  }
}
```

### Use a JSON Validator

Before using your JSON file with Templify, validate it:

1. Go to [jsonlint.com](https://jsonlint.com)
2. Paste your JSON
3. Click "Validate JSON"
4. Fix any errors it reports

### Use a Good Text Editor

Some text editors help you write JSON:

- **VS Code** - Free, shows errors as you type, auto-indents
- **Notepad++** - Free, syntax highlighting
- **Sublime Text** - Free trial, clean interface

Avoid Microsoft Word or rich-text editors - they add invisible formatting that breaks JSON!

### Format for Readability

**Hard to read:**
```json
{"Name":"Alice","Age":25,"City":"NYC"}
```

**Easy to read:**
```json
{
  "Name": "Alice",
  "Age": 25,
  "City": "NYC"
}
```

Most text editors can auto-format JSON for you (often with `Shift+Alt+F` or a "Format Document" command).

## How JSON Maps to Template Placeholders

The structure of your JSON determines how you write placeholders:

### Simple Fields

**JSON:**
```json
{
  "CustomerName": "Alice"
}
```

**Template:**
```
Customer: {{CustomerName}}
```

### Nested Fields

**JSON:**
```json
{
  "Customer": {
    "Name": "Alice",
    "Email": "alice@example.com"
  }
}
```

**Template:**
```
Name: {{Customer.Name}}
Email: {{Customer.Email}}
```

### Lists/Arrays

**JSON:**
```json
{
  "Items": [
    { "Name": "Widget", "Price": 10 },
    { "Name": "Gadget", "Price": 20 }
  ]
}
```

**Template:**
```
{{#foreach Items}}
- {{Name}}: ${{Price}}
{{/foreach}}
```

### Array Item by Index

**JSON:**
```json
{
  "Colors": ["Red", "Green", "Blue"]
}
```

**Template:**
```
First color: {{Colors[0]}}
Second color: {{Colors[1]}}
```

## Quick Reference

| Data Type | JSON Example | Template Example |
|-----------|-------------|------------------|
| Text | `"Name": "Alice"` | `{{Name}}` |
| Number | `"Age": 25` | `{{Age}}` |
| True/False | `"IsActive": true` | `{{#if IsActive}}...{{/if}}` |
| Nested Object | `"Customer": { "Name": "Alice" }` | `{{Customer.Name}}` |
| List | `"Items": ["A", "B"]` | `{{#foreach Items}}{{.}}{{/foreach}}` |

## Next Steps

Now that you understand JSON basics, learn how to use it in your templates:

- **[Placeholders](placeholders.md)** - Using data in your templates
- **[Conditionals](conditionals.md)** - Showing content based on data values
- **[Loops](loops.md)** - Repeating content for lists
- **[Getting Started](getting-started.md)** - Complete beginner tutorial

## Need Help?

If you're stuck:

1. **Validate your JSON** at jsonlint.com
2. **Check for common mistakes** (missing commas, quotes, brackets)
3. **Start simple** and add complexity gradually
4. **Look at examples** in our [Examples Gallery](examples-gallery.md)

Remember: Everyone makes JSON mistakes at first. Use a validator, and you'll get the hang of it quickly!
