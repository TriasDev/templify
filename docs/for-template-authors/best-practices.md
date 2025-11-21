# Best Practices for Template Authors

This guide provides practical tips and recommendations for creating maintainable, efficient, and error-free templates.

## Naming Conventions

### Use Descriptive Names

**❌ Bad:**
```json
{
  "n": "Alice",
  "d": "2024-01-15",
  "amt": 100
}
```

**✅ Good:**
```json
{
  "CustomerName": "Alice",
  "InvoiceDate": "2024-01-15",
  "TotalAmount": 100
}
```

### Use Consistent Naming Styles

Pick a style and stick with it throughout your template:

**PascalCase (recommended):**
```json
{
  "CustomerName": "Alice",
  "OrderDate": "2024-01-15",
  "ShippingAddress": {...}
}
```

**camelCase:**
```json
{
  "customerName": "Alice",
  "orderDate": "2024-01-15",
  "shippingAddress": {...}
}
```

**Avoid:**
- `snake_case` (hard to read in templates)
- `kebab-case` (doesn't work well with dot notation)
- Mixing styles

### Boolean Names Should Be Questions

**✅ Good:**
```json
{
  "IsActive": true,
  "HasDiscount": true,
  "ShouldShowFooter": true,
  "CanEdit": true
}
```

**❌ Less clear:**
```json
{
  "Active": true,
  "Discount": true,
  "Footer": true,
  "Edit": true
}
```

## JSON Structure

### Group Related Data

**❌ Flat structure:**
```json
{
  "CustomerName": "Alice",
  "CustomerEmail": "alice@example.com",
  "CustomerPhone": "+1-555-0123",
  "ShippingStreet": "123 Main St",
  "ShippingCity": "Springfield",
  "ShippingState": "IL",
  "BillingStreet": "456 Oak Ave",
  "BillingCity": "Chicago",
  "BillingState": "IL"
}
```

**✅ Nested structure:**
```json
{
  "Customer": {
    "Name": "Alice",
    "Email": "alice@example.com",
    "Phone": "+1-555-0123"
  },
  "ShippingAddress": {
    "Street": "123 Main St",
    "City": "Springfield",
    "State": "IL"
  },
  "BillingAddress": {
    "Street": "456 Oak Ave",
    "City": "Chicago",
    "State": "IL"
  }
}
```

### Match JSON to Template Usage

Structure your data to match how you'll use it in the template.

**Template needs:**
```
{{#foreach Departments}}
Department: {{Name}}
Employees:
{{#foreach Employees}}
  - {{Name}}
{{/foreach}}
{{/foreach}}
```

**JSON structure should match:**
```json
{
  "Departments": [
    {
      "Name": "Engineering",
      "Employees": [
        { "Name": "Alice" },
        { "Name": "Bob" }
      ]
    }
  ]
}
```

## Template Organization

### Add Comments in Word

Use Word's comment feature to document your templates:

1. Select a placeholder or section
2. Right-click → New Comment
3. Explain what data is expected or what the section does

**Example comments:**
- "This section only shows for VIP customers"
- "Expected format: YYYY-MM-DD"
- "Loop expects array of products with Name, Price, SKU"

### Break Complex Templates into Sections

Use clear section headings in your Word document:

```
=== CUSTOMER INFORMATION ===
{{Customer.Name}}
...

=== ORDER DETAILS ===
{{#foreach Items}}
...
{{/foreach}}

=== PAYMENT INFORMATION ===
...
```

### Keep Line Length Reasonable

**❌ Hard to read:**
```
{{#if Status = "Active" and HasSubscription and not IsExpired and DaysRemaining > 0 and AccountType = "Premium"}}...{{/if}}
```

**✅ Easier to read:**
```
{{#if Status = "Active" and HasSubscription}}
  {{#if not IsExpired and DaysRemaining > 0}}
    {{#if AccountType = "Premium"}}
      Premium content here
    {{/if}}
  {{/if}}
{{/if}}
```

## Testing

### Start with Simple Test Data

Use obvious test values to verify placeholders work:

**test-data.json:**
```json
{
  "CustomerName": "TEST_NAME",
  "Email": "TEST_EMAIL",
  "Phone": "TEST_PHONE"
}
```

If you see "TEST_NAME" in the output, you know the placeholder works!

### Test Edge Cases

Always test with:

**1. Empty arrays:**
```json
{
  "Items": []
}
```

**2. Single-item arrays:**
```json
{
  "Items": [
    { "Name": "Only Item" }
  ]
}
```

**3. Many items:**
```json
{
  "Items": [/* 20+ items */]
}
```

**4. Minimum/maximum values:**
```json
{
  "Age": 0,
  "Score": 100,
  "Temperature": -40
}
```

**5. Missing optional fields:**
```json
{
  "Name": "Alice"
  // MiddleName intentionally missing
}
```

### Validate JSON Before Testing

Always validate your JSON before using it with Templify:

1. Go to [jsonlint.com](https://jsonlint.com)
2. Paste your JSON
3. Click "Validate JSON"
4. Fix any errors

**Common errors:**
- Missing or extra commas
- Missing quotes
- Unclosed brackets/braces

## Error Prevention

### Double-Check Placeholder Names

**JSON keys are case-sensitive:**
- Template: `{{CustomerName}}`
- JSON: `"CustomerName"` ✅
- JSON: `"customername"` ❌
- JSON: `"customer_name"` ❌

Always match the exact case from your JSON data.

### Verify Closing Tags

Every opening tag needs a closing tag:

**❌ Missing closing tag:**
```
{{#if IsActive}}
  Content
← Where's the {{/if}}?
```

**✅ Proper closing:**
```
{{#if IsActive}}
  Content
{{/if}}
```

### Match Brackets and Braces

**❌ Wrong:**
```
{{CustomerName}      ← Missing }
{{CustomerName       ← Missing }}
{CustomerName}}      ← Missing {
```

**✅ Correct:**
```
{{CustomerName}}
```

### Avoid Spaces in Placeholders

**❌ Wrong:**
```
{{ CustomerName }}   ← Spaces inside
{{Customer Name}}    ← Space in name
```

**✅ Correct:**
```
{{CustomerName}}
{{Customer.Name}}    ← Dot notation is OK
```

## Performance Tips

### Keep Templates Reasonably Sized

- Templates under 50 pages process quickly
- Templates 50-200 pages may take a few seconds
- Templates over 200 pages may be slow

Consider splitting very large documents into multiple templates.

### Limit Loop Nesting

**✅ Good (2 levels):**
```
{{#foreach Departments}}
  {{#foreach Employees}}
    ...
  {{/foreach}}
{{/foreach}}
```

**⚠️ Avoid (4+ levels):**
```
{{#foreach A}}
  {{#foreach B}}
    {{#foreach C}}
      {{#foreach D}}
        ... (hard to read and maintain)
      {{/foreach}}
    {{/foreach}}
  {{/foreach}}
{{/foreach}}
```

### Minimize Conditional Complexity

**❌ Complex:**
```
{{#if (Status = "Active" or Status = "Trial") and (Age >= 18 or HasParentConsent) and not (IsBanned or IsExpired) and AccountType = "Premium"}}
```

**✅ Simpler (break into steps):**
```
{{#if Status = "Active" or Status = "Trial"}}
  {{#if Age >= 18 or HasParentConsent}}
    {{#if not IsBanned and not IsExpired}}
      {{#if AccountType = "Premium"}}
        ...
      {{/if}}
    {{/if}}
  {{/if}}
{{/if}}
```

## Formatting Best Practices

### Use Format Specifiers

**❌ Raw values:**
```
Active: {{IsActive}}        → Active: true
Price: {{Price}}            → Price: 1234.5
Date: {{Date}}              → Date: 2024-01-15T00:00:00
```

**✅ Formatted:**
```
Active: {{IsActive:yesno}}  → Active: Yes
Price: {{Price:currency}}   → Price: $1,234.50
Date: {{Date:date:MMM d}}   → Date: Jan 15
```

### Preserve Template Formatting

Templify preserves the formatting of your template text:

- **Bold** text in template stays bold in output
- Font family, size, color are preserved
- Paragraph alignment is maintained
- List formatting (bullets, numbering) is kept

**Tip:** Format your placeholders in Word to get the formatting you want in the output.

### Use Markdown for Dynamic Formatting

When formatting needs to come from data:

**JSON:**
```json
{
  "Message": "Welcome **Alice**, your account is *active*!"
}
```

**Template:**
```
{{Message}}
```

**Output:**
"Welcome **Alice**, your account is *active*!" (with bold and italic applied)

## Maintenance

### Version Your Templates

Keep track of template versions:

**File naming:**
- `invoice-template-v1.docx`
- `invoice-template-v2.docx`
- `invoice-template-2024-01-15.docx`

**Or add version in template:**
```
[Invoice Template v2.3 - Last updated: 2024-01-15]
```

### Document Data Requirements

Create a companion document listing required data:

**invoice-template-DATA.md:**
```markdown
# Invoice Template Data Requirements

## Required Fields
- InvoiceNumber (string)
- InvoiceDate (string, format: YYYY-MM-DD)
- Customer.Name (string)
- Customer.Address (string)
- LineItems (array of objects):
  - Description (string)
  - Quantity (number)
  - UnitPrice (number)
  - Total (number)

## Optional Fields
- Customer.TaxID (string)
- DiscountAmount (number)
- Notes (string)
```

### Keep Examples Updated

Maintain a sample JSON file alongside your template:

**Files:**
- `invoice-template.docx` (the template)
- `invoice-sample-data.json` (sample data)
- `invoice-output-example.docx` (processed example)

## Common Mistakes to Avoid

### 1. Forgetting {{/if}} or {{/foreach}}

**Error:** Template doesn't process correctly

**Solution:** Count your opening and closing tags:
```
{{#if A}}       ← 1 opening
  {{#if B}}     ← 2 openings
  {{/if}}       ← 1 closing
{{/if}}         ← 2 closings ✓
```

### 2. Wrong Array Access

**JSON:**
```json
{
  "Items": [{"Name": "Widget"}]
}
```

**❌ Wrong:**
```
First item: {{Items.Name}}  ← Wrong, Items is array not object
```

**✅ Correct:**
```
First item: {{Items[0].Name}}
Or loop:
{{#foreach Items}}{{Name}}{{/foreach}}
```

### 3. Comparing Wrong Types

**JSON:**
```json
{
  "Age": "25"    ← String!
}
```

**❌ Might not work:**
```
{{#if Age > 18}}  ← Comparing string to number
```

**✅ Better:**
```json
{
  "Age": 25      ← Number (no quotes)
}
```

### 4. Case Mismatches

**JSON:**
```json
{
  "customername": "Alice"
}
```

**❌ Won't match:**
```
{{CustomerName}}  ← Different case
```

**✅ Must match exactly:**
```
{{customername}}
```

### 5. Trailing Commas in JSON

**❌ Invalid JSON:**
```json
{
  "Name": "Alice",
  "Age": 25,     ← Extra comma
}
```

**✅ Valid JSON:**
```json
{
  "Name": "Alice",
  "Age": 25
}
```

## Troubleshooting Checklist

When things don't work:

- [ ] Validate JSON at jsonlint.com
- [ ] Check placeholder spelling matches JSON exactly
- [ ] Verify {{...}} has two braces on each side
- [ ] Confirm all {{#if}} have matching {{/if}}
- [ ] Confirm all {{#foreach}} have matching {{/foreach}}
- [ ] Check for spaces inside braces: `{{Name}}` not `{{ Name }}`
- [ ] Verify array access uses [index] not dot notation
- [ ] Test with simple data first
- [ ] Check that JSON types match expectations (numbers not strings)

## Quick Reference

### DO:
✅ Use descriptive variable names
✅ Validate JSON before testing
✅ Test with edge cases
✅ Comment complex logic in Word
✅ Group related data in JSON
✅ Use format specifiers
✅ Keep templates organized

### DON'T:
❌ Use single-letter variable names
❌ Skip JSON validation
❌ Only test happy path
❌ Create overly complex conditionals
❌ Use inconsistent naming styles
❌ Forget closing tags
❌ Mix up case in placeholder names

## Resources

- **[Template Syntax Reference](template-syntax.md)** - Complete syntax guide
- **[JSON Basics](json-basics.md)** - Understanding JSON
- **[Examples Gallery](examples-gallery.md)** - Real-world examples
- **[Placeholders Guide](placeholders.md)** - Using placeholders effectively
- **[Conditionals Guide](conditionals.md)** - If/else best practices
- **[Loops Guide](loops.md)** - Working with arrays

---

Remember: Good templates are readable, maintainable, and well-tested. Take time to structure your data properly and document your work – your future self (and others) will thank you!
