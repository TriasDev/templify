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

### Placeholder Name Rules

- No spaces inside the braces: `{{Name}}` works, `{{ Name }}` is left unchanged.
- Names and keys may contain letters, digits and underscores (`_`). Dots separate levels, square brackets index arrays or dictionaries (`{{Items[0]}}`, `{{Settings[Theme]}}`).
- Keys with spaces, hyphens or other special characters (`"first-name"`, `"My Key"`) cannot be used in placeholders, not even in brackets. Rename them in your data.
- `{{.}}` or `{{this}}` refers to the current item inside a loop.

---

## Conditionals

Conditionals let you show or hide content based on data values.

> **Block markers go in their own paragraphs.** When `{{#if}}`, `{{#elseif}}`, `{{#else}}` and `{{/if}}` are in separate paragraphs, each marker paragraph is removed completely, including any other text in it. When all markers of a conditional are in **one** paragraph, only that part of the paragraph changes (an [inline conditional](conditionals.md#inline-conditionals)): `Dear {{#if IsVip}}valued {{/if}}customer`.

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
{{#else}}
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
{{#else}}
Your account is inactive.
{{/if}}
```

### Conditional with ElseIf

Use `{{#elseif}}` for multiple conditions:

```
{{#if Condition1}}
  Content for condition 1
{{#elseif Condition2}}
  Content for condition 2
{{#elseif Condition3}}
  Content for condition 3
{{#else}}
  Default content
{{/if}}
```

**JSON:**
```json
{
  "Score": 75
}
```

**Template:**
```
{{#if Score >= 90}}
Grade: A
{{#elseif Score >= 80}}
Grade: B
{{#elseif Score >= 70}}
Grade: C
{{#else}}
Grade: F
{{/if}}
```

**Output:**
```
Grade: C
```

**Note:** The `{{#else}}` branch must always be last.

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
{{#else}}
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

### Loop Markers Need Their Own Paragraphs

A loop repeats whole paragraphs (or table rows). Put `{{#foreach}}` and `{{/foreach}}` in their own paragraphs, above and below the content. The marker paragraphs are removed, including any other text in them. A loop whose markers are both in **one** paragraph (`{{#foreach Tags}}{{.}}, {{/foreach}}`) does not produce any output; to show a list on one line, prepare the joined text in your data (e.g. `"TagList": "red, green, blue"`).

### Loops in Tables

Loops can repeat table rows. Put `{{#foreach}}` and `{{/foreach}}` in **their own rows**, above and below the row(s) to repeat:

**Template (in Word table):**

| Product | Price |
|---------|-------|
| {{#foreach Items}} | |
| {{Name}} | ${{Price}} |
| {{/foreach}} | |

The rows between the marker rows are repeated for each item; the marker rows themselves are removed.

> **Note:** The loop markers must not share a row with the content to repeat. Putting `{{#foreach}}` in the first cell and `{{/foreach}}` in the last cell of the **same row** is an error (processing fails). A loop whose `{{#foreach}}` and `{{/foreach}}` are both inside **one cell** (in separate paragraphs) repeats paragraphs inside that cell instead.

### Conditional Table Rows

Table rows can be shown or hidden with `{{#if}}` / `{{#elseif}}` / `{{#else}}` / `{{/if}}` markers in their own rows:

| Item | Amount |
|------|--------|
| Subtotal | {{Subtotal}} |
| {{#if HasDiscount}} | |
| Discount | {{Discount}} |
| {{/if}} | |
| Total | {{Total}} |

Marker rows are removed; the rows of the matching branch are kept, all other branch rows are removed. Conditional rows also work inside table row loops (evaluated per item). If every row of a table is removed, the table is removed as well.

---

## Operators

### Comparison Operators

| Operator | Meaning | Example |
|----------|---------|---------|
| `=` or `==` | Equal to | `{{#if Status = "Active"}}` |
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

### Membership, String, and Existence Operators

| Operator | Meaning | Example |
|----------|---------|---------|
| `in` | Value is in a collection, list literal, or comma-separated string | `{{#if Role in Roles}}`, `{{#if Role in ("Admin", "Editor")}}` |
| `contains` | Substring check (case-sensitive); on a list: membership | `{{#if Description contains "urgent"}}`, `{{#if Tags contains "urgent"}}` |
| `startswith` | Prefix check (case-sensitive; always false on a list) | `{{#if Code startswith "US-"}}` |
| `endswith` | Suffix check (case-sensitive; always false on a list) | `{{#if FileName endswith ".pdf"}}` |
| `exists` | Variable is present, even if null | `{{#if Notes exists}}` |
| `is empty` | Missing, null, blank, or an empty collection | `{{#if Notes is empty}}` |
| `is not empty` | Has a value | `{{#if Notes is not empty}}` |

Negate `in` with `not`: `{{#if not Role in Roles}}`.

Word operators (`and`, `or`, `not`, `in`, `contains`, ...) are case-insensitive (`AND` works too). `&&`, `||`, `===`, `<>` and `eq`/`ne`/`gt`/... are **not** operators: a condition that uses them cannot be parsed, is treated as false and produces an `ExpressionFailed` warning.

### Operator Precedence

From loosest to tightest binding:

| Level | Operators |
|-------|-----------|
| 1 (loosest) | `or` |
| 2 | `and` |
| 3 | `not` |
| 4 | Comparisons (`=`, `==`, `!=`, `>`, `<`, `>=`, `<=`), `in`, `contains`, `startswith`, `endswith` |
| 5 (tightest) | `exists`, `is empty`, `is not empty` (written after the value) |

Parentheses `()` group explicitly and always win.

- `and` binds tighter than `or`: `A or B and C` means `A or (B and C)`.
- `not` binds **looser** than comparisons: `not Status = "Active"` means `not (Status = "Active")`, and `not Role in Roles` means `not (Role in Roles)`.
- `not` binds tighter than `and`/`or`: `not A and B` means `(not A) and B`.

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
| `:raw` | Insert text without markdown | "my_file.docx" | my_file.docx |
| `:checkbox` | true/false → ☑/☐ | false | ☐ |
| `:number:N2` | Format number with 2 decimals | 1234.5 | 1,234.50 |
| `:currency` | Format as currency (en-US culture) | 1234.5 | $1,234.50 |
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

**Output (en-US culture):**
```
Name: ALICE JOHNSON
Status: Yes
Discount: ☐
Price: $1,234.57
Date: January 15, 2024
```

Each specifier applies to one kind of value (text, boolean, number or date); on other values it is ignored. Numbers stored as text (`"Price": "1234.5"`) are not formatted by `:currency` or `:number`.

For more format specifier details, see [Format Specifiers Guide](format-specifiers.md).

---

## Loop Variables

Special variables available inside loops (only there: outside a loop, `{{@count}}` is not replaced; in nested loops they refer to the innermost loop):

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

### `{{@number}}`

The current item number (starts at 1, i.e. `@index + 1`; since 1.8.0):

**Template:**
```
{{#foreach Items}}
{{@number}}. {{Name}}
{{/foreach}}
```

**Output:**
```
1. Widget
2. Gadget
3. Doohickey
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
{{Name}}{{#if not @last}},{{/if}}
{{/foreach}}
```

**Output** (one paragraph per item):
```
Widget,
Gadget,
Doohickey
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

### Turning Markdown Off

Values such as `my_report_final.docx` or `2*3*4` contain markdown characters. Use `{{FileName:raw}}` to insert a value literally; developers can switch markdown off for all placeholders with the `EnableMarkdown = false` option.

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

Templify preserves the whitespace of your content paragraphs:

```
{{#if IsActive}}
  This line is indented
{{/if}}
```

The marker paragraphs are removed; the content paragraph keeps its indentation.

---

## Quick Syntax Summary

| Feature | Syntax | Example |
|---------|--------|---------|
| Placeholder | `{{Name}}` | `{{CustomerName}}` |
| Nested | `{{Parent.Child}}` | `{{Customer.Address.City}}` |
| Array | `{{Array[0]}}` | `{{Colors[0]}}` |
| If | `{{#if ...}}...{{/if}}` | `{{#if IsActive}}...{{/if}}` |
| If/Else | `{{#if ...}}...{{#else}}...{{/if}}` | See above |
| If/ElseIf | `{{#if ...}}...{{#elseif ...}}...{{/if}}` | See above |
| Loop | `{{#foreach ...}}...{{/foreach}}` | `{{#foreach Items}}...{{/foreach}}` |
| Format | `{{Name:format}}` | `{{Price:currency}}` |
| Loop Index | `{{@index}}` | `{{@index}}` |
| Loop Number | `{{@number}}` | `{{@number}}.` |
| Loop First | `{{@first}}` | `{{#if @first}}...{{/if}}` |
| Loop Last | `{{@last}}` | `{{#if @last}}...{{/if}}` |
| Loop Count | `{{@count}}` | `{{@count}}` |
| Current item | `{{.}}` or `{{this}}` | `{{#foreach Tags}}`…`{{.}}`…`{{/foreach}}` |
| Inline expression | `{{(expression)}}` | `{{(Age >= 18):yesno}}` |

---

## Common Patterns

### Numbered List

```
{{#foreach Items}}
{{@number}}. {{Name}}
{{/foreach}}
```

### List With Separators

```
{{#foreach Tags}}
{{.}}{{#if not @last}};{{/if}}
{{/foreach}}
```

Each item is its own paragraph. For a list on a single line, provide the joined text in your data.

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
  {{#else}}
    Regular content
  {{/if}}
{{#else}}
  Please log in
{{/if}}
```

### Table with Conditional Rows

| Product | Price | Status |
|---------|-------|--------|
| {{#foreach Products}} | | |
| {{#if InStock}} | | |
| {{Name}} | {{Price}} | Available |
| {{#else}} | | |
| {{Name}} | {{Price}} | Out of Stock |
| {{/if}} | | |
| {{/foreach}} | | |

For a single differing cell, an inline conditional inside the cell is simpler:
`| {{Name}} | {{Price}} | {{#if InStock}}Available{{#else}}Out of Stock{{/if}} |`

---

## Best Practices

1. **Match case exactly** - `{{Name}}` must match `"Name"` in JSON
2. **Test with simple data first** - Start with basic JSON and gradually add complexity
3. **Use meaningful names** - `{{CustomerFirstName}}` is better than `{{N1}}`
4. **Validate JSON** - Use jsonlint.com to check JSON syntax
5. **Comment your templates** - Add notes in Word comments about complex logic (comments are not processed, so placeholders in them stay as they are)
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
| Placeholder not replaced | Check that JSON key matches exactly (case-sensitive) and there are no spaces inside the braces |
| Conditional not working | Verify operator syntax and value types |
| Loop not repeating | Ensure JSON has an array for the loop variable and the markers are in their own paragraphs |
| Formatting not applied | Check format specifier syntax: `{{Value:format}}` |
| Syntax error | Validate JSON at jsonlint.com |
| Missing data | Check for typos in placeholder names |

For more help, see [Best Practices](best-practices.md) or [FAQ](../FAQ.md).
