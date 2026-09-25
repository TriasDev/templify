# Getting Started with Templify

Welcome! This guide will help you create your first Word document template with Templify. **No programming experience required** - if you can use Microsoft Word and edit a simple text file, you can create templates.

## What is Templify?

Templify is a tool that takes a Word document template with placeholders (like `{{CompanyName}}`) and fills them in with your data to create personalized documents. Think of it like mail merge, but more powerful.

## What You'll Need

1. **Microsoft Word** (or any app that can edit .docx files)
2. **A text editor** (Notepad, TextEdit, VS Code, or any editor for JSON files)
3. **Templify** - the Templify GUI application, or an application that uses the Templify library (developers can also use the demo console app, see below)

## Your First Template in 5 Minutes

### Step 1: Create a Word Template

1. Open Microsoft Word
2. Create a new document
3. Type some text with placeholders in double curly braces:

```
Hello {{Name}}!

Welcome to {{CompanyName}}. We're excited to have you on board.

Your account has been created with the email: {{Email}}
```

4. Save the document as `welcome-letter.docx`

**That's it!** You've created your first template. The text inside `{{...}}` are placeholders that will be replaced with actual data.

### Step 2: Prepare Your Data (JSON)

Now you need to provide the data to fill in those placeholders. We use a format called JSON (don't worry, it's simple!).

Create a file called `data.json` with this content:

```json
{
  "Name": "Alice Johnson",
  "CompanyName": "Acme Corporation",
  "Email": "alice.johnson@acme.com"
}
```

**Understanding the structure:**
- The whole thing is wrapped in `{ }` (curly braces)
- Each piece of data is written as `"PlaceholderName": "Value"`
- Separate each piece with a comma
- Text values need double quotes around them

### Step 3: Process Your Template

Now you'll combine the template with the data to create the final document.

#### Option A: Using the GUI Application

1. Open the Templify GUI application
2. Next to **Template File**, click **Browse...** and choose `welcome-letter.docx`
3. Next to **JSON Data**, click **Browse...** and choose `data.json`
4. Check or change the **Output File** path (for example `welcome-letter-final.docx`)
5. Click **Process Template**

#### Option B: Using the Command Line (from the source code)

Templify has no installable `templify` command. If you have the [.NET SDK](https://dotnet.microsoft.com/download) and a copy of the [Templify repository](https://github.com/TriasDev/templify), the demo app processes your own files:

```bash
dotnet run --project TriasDev.Templify.Demo -- --template welcome-letter.docx --data data.json --output welcome-letter-final.docx
```

In most setups, a developer integrates Templify into an application, and you only provide the template and the data.

### Step 4: View the Result

Open `welcome-letter-final.docx` in Word. You should see:

```
Hello Alice Johnson!

Welcome to Acme Corporation. We're excited to have you on board.

Your account has been created with the email: alice.johnson@acme.com
```

**Congratulations!** You've created and processed your first template! 🎉

## What You Can Do With Templates

### Simple Placeholders

Replace any text with data from your JSON file:

**Template:**
```
Customer: {{CustomerName}}
Order Number: {{OrderNumber}}
Date: {{OrderDate}}
```

**Data (data.json):**
```json
{
  "CustomerName": "Bob Smith",
  "OrderNumber": "ORD-12345",
  "OrderDate": "2024-01-15"
}
```

### Nested Data

Access data within data using dots (`.`):

**Template:**
```
Name: {{Customer.Name}}
City: {{Customer.Address.City}}
Country: {{Customer.Address.Country}}
```

**Data (data.json):**
```json
{
  "Customer": {
    "Name": "Sarah Connor",
    "Address": {
      "City": "Los Angeles",
      "Country": "USA"
    }
  }
}
```

### Conditional Content

Show or hide content based on conditions:

**Template:**
```
{{#if IsPremium}}
Thank you for being a Premium member!
{{/if}}

{{#if Status = "Active"}}
Your account is active.
{{#else}}
Your account needs activation.
{{/if}}
```

**Data (data.json):**
```json
{
  "IsPremium": true,
  "Status": "Active"
}
```

### Repeating Content (Loops)

Repeat content for each item in a list:

**Template:**
```
Your order contains:

{{#foreach Items}}
- {{Name}}: ${{Price}}
{{/foreach}}
```

**Data (data.json):**
```json
{
  "Items": [
    { "Name": "Widget", "Price": "10.00" },
    { "Name": "Gadget", "Price": "25.00" },
    { "Name": "Doohickey", "Price": "15.00" }
  ]
}
```

**Result:**
```
Your order contains:

- Widget: $10.00
- Gadget: $25.00
- Doohickey: $15.00
```

## Common Mistakes to Avoid

### ❌ Wrong Placeholder Syntax

```
{Name}           ← Only one curly brace (needs two)
{{Name}          ← Missing closing braces
{{ Name }}       ← Spaces inside (remove them; it is not replaced)
{{First Name}}   ← Spaces or hyphens in the name are not allowed
```

### ✅ Correct Placeholder Syntax

```
{{Name}}         ← Perfect!
```

### ❌ Invalid JSON

```json
{
  Name: "Alice"          ← Missing quotes around the key
  "Email": alice@...     ← Missing quotes around the value
  "Age": 25,             ← Extra comma at the end
}
```

### ✅ Valid JSON

```json
{
  "Name": "Alice",
  "Email": "alice@email.com",
  "Age": 25
}
```

**Tip:** Use a JSON validator website (like jsonlint.com) to check your JSON files if you get errors.

## Next Steps

Now that you've created your first template, explore more advanced features:

- **[JSON Basics](json-basics.md)** - Learn more about JSON data format
- **[Placeholders](placeholders.md)** - All the ways to use placeholders
- **[Conditionals](conditionals.md)** - Show/hide content with if/else
- **[Loops](loops.md)** - Repeat content for lists and tables
- **[Format Specifiers](format-specifiers.md)** - Format numbers, dates, and more
- **[Best Practices](best-practices.md)** - Tips for creating maintainable templates

## Need Help?

- **Can't find your placeholder?** Make sure the name in `{{...}}` exactly matches the name in your JSON (including uppercase/lowercase)
- **Getting an error?** Check that your JSON is valid using a JSON validator
- **Template not changing?** Make sure you're opening the **output** file, not the original template

For more help, check out our [Examples Gallery](examples-gallery.md) with downloadable templates you can study and modify.
