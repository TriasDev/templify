# Template Syntax Reference

This is a complete reference guide for Templify's template syntax. Use this as a quick lookup when creating templates.

## Table of Contents

- [Placeholders](#placeholders)
- [Conditionals](#conditionals)
- [Loops](#loops)
- [Operators](#operators)
- [Format Specifiers](#format-specifiers)
- [Loop Variables](#loop-variables)
- [Markdown Formatting](#markdown-formatting)
- [Line Breaks in Data](#line-breaks-in-data)

---

## Placeholders

Placeholders are replaced with data from your JSON file. They're wrapped in double curly braces: `{{...}}`

### Simple Placeholder

```
{{VariableName}}
```

**JSON:**
```json
{
  "CustomerName": "Alice Johnson"
}
```

**Template:**
```
Customer: {{CustomerName}}
```

**Output:**
```
Customer: Alice Johnson
```

### Nested Properties (Dot Notation)

Access nested data using dots (`.`):

```
{{Parent.Child.Property}}
```

**JSON:**
```json
{
  "Customer": {
    "Name": "Bob Smith",
    "Address": {
      "City": "New York",
      "Zip": "10001"
    }
  }
}
```

**Template:**
```
Name: {{Customer.Name}}
City: {{Customer.Address.City}}
ZIP: {{Customer.Address.Zip}}
```

### Array Indexing

Access specific array items by index (starting at 0):

```
{{ArrayName[Index]}}
{{ArrayName[0].Property}}
```

**JSON:**
```json
{
  "Colors": ["Red", "Green", "Blue"],
  "Users": [
    { "Name": "Alice", "Age": 25 },
    { "Name": "Bob", "Age": 30 }
  ]
}
```

**Template:**
```
First color: {{Colors[0]}}
Second user: {{Users[1].Name}}, age {{Users[1].Age}}
```

### Case Sensitivity

JSON keys are **case-sensitive**. `{{Name}}` and `{{name}}` are different. Always match the exact case used in your JSON data.

---

## Conditionals

Conditionals let you show or hide content based on data values.

### Basic Conditional

```
{{#if VariableName}}
  Content to show if true
{{/if}}
```

**JSON:**
```json
{
  "IsVIP": true
}
```

**Template:**
```
{{#if IsVIP}}
Thank you for being a VIP member!
{{/if}}
```

### Conditional with Else

```
{{#if Condition}}
  Content when true
{{else}}
  Content when false
{{/if}}
```

**JSON:**
```json
{
  "Status": "Active"
}
```

**Template:**
```
{{#if Status = "Active"}}
Your account is active.
{{else}}
Your account is inactive.
{{/if}}
```

### Conditional with Comparisons

```
{{#if Variable operator Value}}
  Content
{{/if}}
```

**Template:**
```
{{#if Age >= 18}}
You are an adult.
{{/if}}

{{#if Score > 90}}
Excellent!
{{else}}
Keep trying!
{{/if}}
```

### Multiple Conditions

Use `and` or `or` to combine conditions:

```
{{#if Condition1 and Condition2}}
  Both are true
{{/if}}

{{#if Condition1 or Condition2}}
  At least one is true
{{/if}}
```

**JSON:**
```json
{
  "Age": 25,
  "HasLicense": true,
  "Country": "USA"
}
```

**Template:**
```
{{#if Age >= 18 and HasLicense}}
You can rent a car.
{{/if}}

{{#if Country = "USA" or Country = "Canada"}}
North American customer
{{/if}}
```

### Negation

Use `not` to negate a condition:

```
{{#if not IsExpired}}
  Subscription is active
{{/if}}

{{#if not (Age < 18)}}
  You are an adult
{{/if}}
```

---

## Loops

Loops repeat content for each item in an array.

### Basic Loop

```
{{#foreach ArrayName}}
  Content to repeat
{{/foreach}}
```

**JSON:**
```json
{
  "Products": [
    { "Name": "Widget", "Price": 10 },
    { "Name": "Gadget", "Price": 20 }
  ]
}
```

**Template:**
```
Product List:

{{#foreach Products}}
- {{Name}}: ${{Price}}
{{/foreach}}
```

**Output:**
```
Product List:

- Widget: $10
- Gadget: $20
```

### Nested Loops

You can nest loops within each other:

**JSON:**
```json
{
  "Departments": [
    {
      "Name": "Sales",
      "Employees": [
        { "Name": "Alice" },
        { "Name": "Bob" }
      ]
    },
    {
      "Name": "Engineering",
      "Employees": [
        { "Name": "Charlie" },
        { "Name": "Diana" }
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
  - {{Name}}
{{/foreach}}

{{/foreach}}
```

### Loops in Tables

Loops can repeat table rows:

**Template (in Word table):**

| Product | Price |
|---------|-------|
| {{#foreach Items}}{{Name}} | ${{Price}}{{/foreach}} |

The row will be repeated for each item.

---

## Operators

### Comparison Operators

| Operator | Meaning | Example |
|----------|---------|---------|
| `=` | Equal to | `{{#if Status = "Active"}}` |
| `!=` | Not equal to | `{{#if Status != "Pending"}}` |
| `>` | Greater than | `{{#if Age > 18}}` |
| `<` | Less than | `{{#if Price < 100}}` |
| `>=` | Greater than or equal | `{{#if Score >= 90}}` |
| `<=` | Less than or equal | `{{#if Stock <= 10}}` |

### Logical Operators

| Operator | Meaning | Example |
|----------|---------|---------|
| `and` | Both conditions true | `{{#if Age >= 18 and HasLicense}}` |
| `or` | At least one true | `{{#if IsVIP or IsPremium}}` |
| `not` | Negates condition | `{{#if not IsExpired}}` |

### Operator Precedence

1. Parentheses `()`
2. `not`
3. Comparison operators (`=`, `!=`, `>`, etc.)
4. `and`
5. `or`

**Example:**
```
{{#if (Age > 18 or HasParent) and not IsBanned}}
  Can enter
{{/if}}
```

---

## Format Specifiers

Format specifiers control how values are displayed. Add them after a colon (`:`) in the placeholder.

### Basic Syntax

```
{{VariableName:format}}
```

### Common Formats

| Format | Description | Example Input | Example Output |
|--------|-------------|---------------|----------------|
| `:uppercase` | Convert to UPPERCASE | "hello" | HELLO |
| `:lowercase` | Convert to lowercase | "HELLO" | hello |
| `:yesno` | true/false → Yes/No | true | Yes |
| `:checkbox` | true/false → ☑/☐ | false | ☐ |
| `:number:N2` | Format number with 2 decimals | 1234.5 | 1,234.50 |
| `:currency` | Format as currency | 1234.5 | $1,234.50 |
| `:date:yyyy-MM-dd` | Format date | (date value) | 2024-01-15 |

### Examples

**JSON:**
```json
{
  "CustomerName": "alice johnson",
  "IsActive": true,
  "HasDiscount": false,
  "Price": 1234.567,
  "OrderDate": "2024-01-15"
}
```

**Template:**
```
Name: {{CustomerName:uppercase}}
Status: {{IsActive:yesno}}
Discount: {{HasDiscount:checkbox}}
Price: {{Price:currency}}
Date: {{OrderDate:date:MMMM d, yyyy}}
```

**Output:**
```
Name: ALICE JOHNSON
Status: Yes
Discount: ☐
Price: $1,234.57
Date: January 15, 2024
```

For more format specifier details, see [Format Specifiers Guide](format-specifiers.md).

---

## Loop Variables

Special variables available inside loops:

### `{{@index}}`

The current loop iteration index (starts at 0):

**Template:**
```
{{#foreach Items}}
Item {{@index}}: {{Name}}
{{/foreach}}
```

**Output:**
```
Item 0: Widget
Item 1: Gadget
Item 2: Doohickey
```

### `{{@first}}`

True if this is the first iteration:

**Template:**
```
{{#foreach Items}}
{{#if @first}}
*** FIRST ITEM ***
{{/if}}
{{Name}}
{{/foreach}}
```

### `{{@last}}`

True if this is the last iteration:

**Template:**
```
{{#foreach Items}}
{{Name}}{{#if not @last}}, {{/if}}
{{/foreach}}
```

**Output:**
```
Widget, Gadget, Doohickey
```

### `{{@count}}`

Total number of items in the loop:

**Template:**
```
{{#foreach Items}}
Processing item {{@index}} of {{@count}}...
{{/foreach}}
```

**Output:**
```
Processing item 0 of 3...
Processing item 1 of 3...
Processing item 2 of 3...
```

---

## Markdown Formatting

Apply formatting to text using markdown syntax in your JSON data:

### Bold

```json
{
  "Message": "This is **bold** text"
}
```

Or use underscores:
```json
{
  "Message": "This is __bold__ text"
}
```

### Italic

```json
{
  "Message": "This is *italic* text"
}
```

Or use underscores:
```json
{
  "Message": "This is _italic_ text"
}
```

### Strikethrough

```json
{
  "Message": "This is ~~strikethrough~~ text"
}
```

### Bold + Italic

```json
{
  "Message": "This is ***bold and italic*** text"
}
```

### Combining with Template Formatting

The markdown formatting is **merged** with the template's formatting. If your template has red text, and you add `**bold**` in the data, the output will be **red bold text**.

---

## Line Breaks in Data

Newline characters in your JSON data are automatically converted to line breaks in Word:

```json
{
  "Address": "123 Main Street\nApartment 4B\nNew York, NY 10001"
}
```

**Supported formats:**
- `\n` - Unix/Linux/macOS
- `\r\n` - Windows
- `\r` - Legacy Mac

### Combining with Markdown

Line breaks work together with markdown formatting:

```json
{
  "Steps": "**Step 1:** Download\n**Step 2:** Install\n**Step 3:** Run"
}
```

Output will have three lines, each with bold text for "Step X:".

---

## Special Characters

### Literal Curly Braces

To include literal `{{` or `}}` in your document without creating a placeholder, there's currently no escape mechanism. Best practice: avoid using `{{` in your document text unless it's a placeholder.

### Whitespace

Templify preserves whitespace in your templates:

```
{{#if IsActive}}
  This line is indented
{{/if}}
```

Will output with the indentation preserved.

---

## Quick Syntax Summary

| Feature | Syntax | Example |
|---------|--------|---------|
| Placeholder | `{{Name}}` | `{{CustomerName}}` |
| Nested | `{{Parent.Child}}` | `{{Customer.Address.City}}` |
| Array | `{{Array[0]}}` | `{{Colors[0]}}` |
| If | `{{#if ...}}...{{/if}}` | `{{#if IsActive}}...{{/if}}` |
| If/Else | `{{#if ...}}...{{else}}...{{/if}}` | See above |
| Loop | `{{#foreach ...}}...{{/foreach}}` | `{{#foreach Items}}...{{/foreach}}` |
| Format | `{{Name:format}}` | `{{Price:currency}}` |
| Loop Index | `{{@index}}` | `{{@index}}` |
| Loop First | `{{@first}}` | `{{#if @first}}...{{/if}}` |
| Loop Last | `{{@last}}` | `{{#if @last}}...{{/if}}` |
| Loop Count | `{{@count}}` | `{{@count}}` |

---

## Common Patterns

### Numbered List

```
{{#foreach Items}}
{{@index}}. {{Name}}
{{/foreach}}
```

### Comma-Separated List

```
{{#foreach Tags}}{{.}}{{#if not @last}}, {{/if}}{{/foreach}}
```

### Conditional with Multiple Checks

```
{{#if Age >= 18 and HasLicense and not IsSuspended}}
Eligible to drive
{{/if}}
```

### Nested Conditionals

```
{{#if IsLoggedIn}}
  {{#if IsPremium}}
    Premium content
  {{else}}
    Regular content
  {{/if}}
{{else}}
  Please log in
{{/if}}
```

### Table with Conditional Rows

```
| Product | Price | Status |
|---------|-------|--------|
{{#foreach Products}}
| {{Name}} | {{Price}} | {{#if InStock}}Available{{else}}Out of Stock{{/if}} |
{{/foreach}}
```

---

## Best Practices

1. **Match case exactly** - `{{Name}}` must match `"Name"` in JSON
2. **Test with simple data first** - Start with basic JSON and gradually add complexity
3. **Use meaningful names** - `{{CustomerFirstName}}` is better than `{{N1}}`
4. **Validate JSON** - Use jsonlint.com to check JSON syntax
5. **Comment your templates** - Add notes in Word comments about complex logic
6. **Keep conditionals simple** - Break complex logic into multiple simpler conditionals

---

## Next Steps

- **[Placeholders Guide](placeholders.md)** - Deep dive into placeholder usage
- **[Conditionals Guide](conditionals.md)** - Detailed conditional examples
- **[Loops Guide](loops.md)** - Advanced loop techniques
- **[Format Specifiers](format-specifiers.md)** - Complete formatting reference
- **[Examples Gallery](examples-gallery.md)** - Real-world template examples

---

## Quick Troubleshooting

| Problem | Solution |
|---------|----------|
| Placeholder not replaced | Check that JSON key matches exactly (case-sensitive) |
| Conditional not working | Verify operator syntax and value types |
| Loop not repeating | Ensure JSON has an array for the loop variable |
| Formatting not applied | Check format specifier syntax: `{{Value:format}}` |
| Syntax error | Validate JSON at jsonlint.com |
| Missing data | Check for typos in placeholder names |

For more help, see [Best Practices](best-practices.md) or [FAQ](../FAQ.md).
