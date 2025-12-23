# Templify Documentation

Welcome to **Templify** - a powerful tool for creating dynamic Word documents from templates with placeholders, conditionals, and loops.

---

## 👥 Choose Your Path

### 📝 I Create Word Templates

**I design Word documents and want to add dynamic placeholders.**

I work with Word documents and need to create templates with placeholders like `{{CustomerName}}` that get filled in with data. I don't need to write code - I just need to know how to structure my templates and data.

**→ [Get Started as a Template Author](for-template-authors/getting-started.md)**

**Quick Links:**
- [JSON Basics](for-template-authors/json-basics.md) - Understanding your data format
- [Template Syntax Reference](for-template-authors/template-syntax.md) - Complete syntax guide
- [Examples Gallery](for-template-authors/examples-gallery.md) - Real-world templates
- [Best Practices](for-template-authors/best-practices.md) - Tips for great templates

---

### 💻 I'm a Developer

**I'm integrating Templify into my .NET application.**

I'm a software developer who wants to use the Templify library in my C# application to programmatically generate Word documents from templates.

**→ [Get Started as a Developer](for-developers/quick-start.md)** *(Coming soon)*

**Quick Links:**
- [Installation Guide](for-developers/installation.md) *(Coming soon)*
- [API Reference](for-developers/api-reference.md) *(Coming soon)*
- [Code Examples](for-developers/examples.md) *(Coming soon)*
- [Architecture Overview](for-developers/architecture.md) *(Coming soon)*

---

## What is Templify?

Templify lets you create Word document templates with special placeholders that get replaced with actual data. Perfect for generating:

- **Invoices & Receipts** - Customer invoices with line items
- **Reports** - Formatted reports from database data
- **Contracts** - Contracts with dynamic clauses
- **Letters** - Mail merge for personalized letters
- **Certificates** - Batch-generated certificates

## Key Features

✨ **Simple Placeholders** - `{{VariableName}}` syntax
🔁 **Loops** - Repeat sections with `{{#foreach}}...{{/foreach}}`
⚡ **Conditionals** - Dynamic content with `{{#if}}...{{#else}}...{{/if}}`
📊 **Table Support** - Loop through table rows
🎨 **Formatting** - Preserves Word styling and supports markdown
🚀 **No Word Required** - Uses Open XML SDK (template authors still use Word to create templates)

## Quick Example

### Template (in Word):

```
Invoice for {{CustomerName}}
Date: {{InvoiceDate}}

Items:
{{#foreach Items}}
- {{Product}}: {{Price}}
{{/foreach}}
```

### Data (JSON):

```json
{
  "CustomerName": "John Doe",
  "InvoiceDate": "2024-01-15",
  "Items": [
    { "Product": "Service A", "Price": "$100" },
    { "Product": "Service B", "Price": "$200" }
  ]
}
```

### Output:

```
Invoice for John Doe
Date: 2024-01-15

Items:
- Service A: $100
- Service B: $200
```

---

## Additional Resources

### ❓ [FAQ](FAQ.md)
Common questions and troubleshooting tips

### 🎓 [Tutorials](tutorials/)
Step-by-step guides from basics to advanced features

### 📖 [Quick Start Guide](quick-start.md)
Create your first document in 5 minutes

## Open Source

Templify is open source and licensed under the [MIT License](https://github.com/triasdev/templify/blob/main/LICENSE).

## Support & Community

- 📖 [Documentation](for-template-authors/getting-started.md)
- 🐛 [Report Issues](https://github.com/triasdev/templify/issues)
- 💬 [Discussions](https://github.com/triasdev/templify/discussions)
- 🌟 [Star on GitHub](https://github.com/triasdev/templify)
