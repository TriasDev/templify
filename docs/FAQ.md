# Frequently Asked Questions (FAQ)

Common questions and answers about using Templify for Word document templating.

---

## Table of Contents

- [Getting Started](#getting-started)
- [Features & Capabilities](#features-capabilities)
- [Syntax & Usage](#syntax-usage)
- [Performance](#performance)
- [Troubleshooting](#troubleshooting)
- [Migration](#migration)
- [Enterprise & Production](#enterprise-production)
- [Comparison](#comparison)

---

## Getting Started

### Q: What is Templify?

**A:** Templify is a modern .NET library for generating Word documents from templates. You create a Word template with placeholders like `{{Name}}`, and Templify replaces them with your data. It supports conditionals, loops, and nested data structures without requiring Microsoft Word to be installed.

### Q: What are the system requirements?

**A:**
- **.NET 8.0 or later** (targets net8.0, net9.0 and net10.0; net6.0 is supported up to Templify 1.7.x)
- **DocumentFormat.OpenXml 3.5.1** (automatically installed via NuGet)
- Any platform supported by .NET (Windows, macOS, Linux)
- **No Microsoft Word installation required**

**Support policy:** we support the .NET versions that are in [Microsoft support](https://dotnet.microsoft.com/platform/support/policy/dotnet-core). Target frameworks that reach end of life are dropped in a minor release, announced in the release notes (net6.0 was dropped in 1.8.0).

### Q: How do I install Templify?

**A:** Via NuGet:
```bash
dotnet add package TriasDev.Templify
```

Or in Visual Studio: `Install-Package TriasDev.Templify`

### Q: Where should I start?

**A:** Follow this learning path:
1. [Quick Start Guide](for-developers/quick-start.md) (5 minutes)
2. [Tutorial 1: Hello World](tutorials/01-hello-world.md) (30 min)
3. [Tutorial 2: Invoice Generator](tutorials/02-invoice-generator.md) (1 hour)
4. [Template Syntax](for-template-authors/template-syntax.md) and the [library README](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/README.md)

### Q: Do I need Microsoft Word installed?

**A:** **No!** Templify uses the OpenXML SDK to work directly with .docx files. It runs on any platform without Office installed.

### Q: Can I write templates in LibreOffice?

**A:** **Yes.** Save the template as OpenDocument Text (`.odt`) or as a template (`.ott`) and process it with
`OdtTemplateProcessor`, or with `TemplateProcessor`, which accepts both Word and OpenDocument files. The template
syntax is the same, and LibreOffice does not need to be installed where the templates are processed. See
[LibreOffice / OpenDocument Templates](for-template-authors/libreoffice.md) and
[OpenDocument for developers](for-developers/opendocument.md).

### Q: Can I use Templify in web applications?

**A:** **Yes!** Templify works perfectly in:
- ASP.NET Core web APIs
- Blazor applications
- Azure Functions
- Background job processors
- Desktop applications
- Console applications

---

## Features & Capabilities

### Q: What features does Templify support?

**A:** Core features:
- ✅ **Placeholders**: `{{Name}}`, `{{User.Email}}`
- ✅ **Nested data**: `{{Company.Address.City}}`
- ✅ **Array indexing**: `{{Items[0].Name}}`
- ✅ **Boolean format specifiers**: `{{IsActive:checkbox}}`, `{{IsVerified:yesno}}`
- ✅ **Boolean expressions**: `{{(Age >= 18 and HasLicense):yesno}}`
- ✅ **Conditionals**: `{{#if IsActive}}...{{#elseif IsPending}}...{{#else}}...{{/if}}`
- ✅ **Loops**: `{{#foreach Items}}...{{/foreach}}`, with named iteration variables: `{{#foreach item in Items}}`
- ✅ **Nested loops**: Loops inside loops (arbitrary depth)
- ✅ **Table row loops**: Dynamic table generation
- ✅ **Loop variables**: `@index`, `@number`, `@first`, `@last`, `@count`
- ✅ **Formatting preservation**: Bold, italic, colors, fonts maintained
- ✅ **Comparison operators**: `>`, `<`, `>=`, `<=`, `==`, `!=`
- ✅ **Logical operators**: `and`, `or`, `not`
- ✅ **Membership, string, and existence operators**: `in`, `contains`, `startswith`, `endswith`, `exists`, `is empty`, `is not empty`
- ✅ **Grouping**: parentheses `()` for controlling evaluation order
- ✅ **Number, date and string formats**: `{{Amount:currency}}`, `{{Value:number:N2}}`, `{{Date:date:yyyy-MM-dd}}`, `{{Name:uppercase}}`
- ✅ **Markdown in values**: `**bold**`, `*italic*`, `~~strike~~` (opt out with `EnableMarkdown = false` or `{{Value:raw}}`)
- ✅ **Localization**: Boolean formats adapt to the culture (en, de, fr, es, it, pt, nl, pl, ru; `yesno` also ja, zh)
- ✅ **JSON support**: Use JSON data instead of C# dictionaries
- ✅ **Headers, footers, footnotes, endnotes, text boxes and content controls** are processed (comments are not)
- ✅ **Plain-text templates**: `TextTemplateProcessor` for emails and other text output

### Q: What is NOT supported?

**A:** Current limitations:
- ❌ **Mathematical expressions in templates**: Can't do `{{Price * Quantity}}` (calculate in code)
- ❌ **String manipulation in templates**: Can't do `{{Name.ToUpper()}}` (format in code)
- ❌ **Image insertion**: No direct image placeholders yet
- ❌ **Chart/graph generation**: Not supported
- ❌ **Macro execution**: Security limitation
- ❌ **Form fields**: Use placeholders instead
- ❌ **Complex formatting changes**: Background colors, page breaks (use template structure)

**Workaround**: Calculate/format values in your C# code before passing to Templify.

### Q: Can Templify handle large documents?

**A:** **Yes!** Templify is designed for performance:
- The whole document is loaded into memory, so it is not suited for documents larger than ~50 MB
- Processing time grows linearly with the size of the document and the data
- See the [performance notes](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/PERFORMANCE.md) (a benchmark snapshot)

### Q: Does Templify support tables?

**A:** **Yes!** Full table support:
- Replace placeholders in table cells
- Loop over table rows: `{{#foreach Items}}` and `{{/foreach}}` in their own rows
- Conditional rows: `{{#if ...}}` / `{{/if}}` in their own rows
- Loops and conditionals inside a single cell
- Nested tables
- Formatting preservation

A table whose rows are all removed (an empty row loop or a false row conditional) is removed entirely, because
Word rejects tables without rows.

### Q: Can I use Templify for mail merge?

**A:** **Absolutely!** Common mail merge scenario:

```csharp
var recipients = GetRecipients(); // List of people

foreach (var recipient in recipients)
{
    var data = new Dictionary<string, object>
    {
        ["Name"] = recipient.Name,
        ["Address"] = recipient.Address,
        ["City"] = recipient.City
    };

    using var templateStream = File.OpenRead("letter-template.docx");
    using var outputStream = File.Create($"letter-{recipient.Id}.docx");

    processor.ProcessTemplate(templateStream, outputStream, data);
}
```

---

## Syntax & Usage

### Q: What syntax do I use for placeholders?

**A:** Use double curly braces:
- Simple: `{{Name}}`
- Nested: `{{User.Email}}`
- Array: `{{Items[0]}}`
- Dictionary key: `{{Settings[Theme]}}` or `{{Settings.Theme}}`

**Rules**:
- No spaces inside the braces: `{{ Name }}` is not a placeholder and stays as text
- Names consist of letters, digits and underscores, joined by dots and `[...]` indexers; keys with spaces or other characters (`{{Settings[My Key]}}`) are not supported
- Dictionary keys are matched with the dictionary's comparer (case-sensitive for a normal `Dictionary<string, object>` and for JSON data); object properties are matched case-insensitively

### Q: How do I access nested properties?

**A:** Use dot notation:

**Data**:
```csharp
var data = new Dictionary<string, object>
{
    ["Company"] = new
    {
        Name = "Acme Corp",
        Address = new
        {
            Street = "123 Main St",
            City = "Springfield"
        }
    }
};
```

**Template**:
```
Company: {{Company.Name}}
Location: {{Company.Address.City}}
```

### Q: How do conditionals work?

**A:** Use `{{#if}}...{{#else}}...{{/if}}`:

```
{{#if IsActive}}
This customer is active.
{{#else}}
This customer is inactive.
{{/if}}
```

**Supported conditions**:
- Boolean: `{{#if IsActive}}`
- Comparison: `{{#if Age > 18}}`
- Null check: `{{#if Email}}` (false if missing, null, empty, `"0"` or `"false"`)
- Combined: `{{#if Age >= 18 and HasLicense}}` (use `and`/`or`/`not`; `&&` and `||` are not operators)
- Chains: `{{#if A}}...{{#elseif B}}...{{#else}}...{{/if}}`

The `{{#if}}`, `{{#else}}` and `{{/if}}` markers can be in their own paragraphs, or all inside one paragraph for
an inline conditional (`Dear {{#if IsVip}}valued {{/if}}customer`).

### Q: How do loops work?

**A:** Use `{{#foreach}}...{{/foreach}}`:

**Template**:
```
{{#foreach Items}}
- {{Name}}: {{Price}} EUR
{{/foreach}}
```

**Data**:
```csharp
["Items"] = new List<object>
{
    new { Name = "Product A", Price = 10.00m },
    new { Name = "Product B", Price = 20.00m }
}
```

**Output** (the number format follows `PlaceholderReplacementOptions.Culture`):
```
- Product A: 10.00 EUR
- Product B: 20.00 EUR
```

The `{{#foreach}}` and `{{/foreach}}` markers must each be in their own paragraph: the paragraph that contains a
marker is removed, together with any other text in it. A loop written inside a single paragraph
(`{{#foreach Tags}}{{.}}, {{/foreach}}`) is not supported in Word templates. For a collection of simple values,
use `{{.}}` (or `{{this}}`) for the current item.

### Q: What are loop special variables?

**A:** Inside loops (only there; outside a loop they are missing variables), use these:
- `{{@index}}` - Current position (0-based)
- `{{@number}}` - Current position (1-based)
- `{{@first}}` - True for first item
- `{{@last}}` - True for last item
- `{{@count}}` - Total number of items

**Example**:
```
{{#foreach Items}}
Item {{@number}}: {{Name}}{{#if @last}} (last){{/if}}
{{/foreach}}
```

In nested loops the metadata refers to the innermost loop.

### Q: Can I nest loops?

**A:** **Yes!** Unlimited nesting. Use named iteration variables to reach the outer item:

```
{{#foreach order in Orders}}
Order #{{order.OrderId}}:
{{#foreach item in order.Items}}
- {{order.OrderId}} / {{item.Product}}: {{item.Quantity}} x {{item.Price}}
{{/foreach}}
{{/foreach}}
```

### Q: How do I use loops in tables?

**A:** Place loop markers in table rows:

| Product | Quantity | Price |
|---------|----------|-------|
| {{#foreach Items}} | | |
| {{Name}} | {{Quantity}} | {{Price}} |
| {{/foreach}} | | |

Templify repeats the rows between the marker rows for each item and removes the marker rows. The markers must be
in separate rows: a `{{#foreach}}` whose `{{/foreach}}` is in another cell of the same row fails processing.

### Q: How do I handle missing variables?

**A:** Check `ProcessingResult`:

```csharp
var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.MissingVariables.Any())
{
    Console.WriteLine("Warning - variables not found:");
    foreach (var variable in result.MissingVariables)
    {
        Console.WriteLine($"  - {variable}");
    }
}
```

By default missing variables are left as-is: `{{MissingVar}}` remains in output. Set
`MissingVariableBehavior` to `ReplaceWithEmpty` to remove them, or to `ThrowException` to throw an
`InvalidOperationException`. `result.Warnings` has one `MissingVariable` warning per occurrence, and
`ValidateTemplate(template, data)` lists missing variables without processing the document.

### Q: How do I format boolean values as checkboxes or Yes/No?

**A:** Use format specifiers with the `:format` syntax:

**Template**:
```
Task complete: {{IsCompleted:checkbox}}
Approved: {{IsApproved:yesno}}
Valid: {{IsValid:checkmark}}
```

**C# Data**:
```csharp
var data = new Dictionary<string, object>
{
    ["IsCompleted"] = true,
    ["IsApproved"] = false,
    ["IsValid"] = true
};
```

**JSON Data**:
```json
{
  "IsCompleted": true,
  "IsApproved": false,
  "IsValid": true
}
```

**Output**:
```
Task complete: ☑
Approved: No
Valid: ✓
```

**Available formatters**:
- `checkbox` → ☑/☐
- `yesno` → Yes/No
- `checkmark` → ✓/✗
- `truefalse` → True/False
- `onoff` → On/Off
- `enabled` → Enabled/Disabled
- `active` → Active/Inactive

See the [Format Specifiers Guide](for-template-authors/format-specifiers.md) for complete documentation.

### Q: Can I use JSON instead of C# dictionaries?

**A:** **Yes!** Every `ProcessTemplate`/`ProcessTemplateFile` overload has a variant that takes a JSON string:

**JSON Data File** (`data.json`):
```json
{
  "CompanyName": "Acme Corp",
  "IsActive": true,
  "Items": [
    { "Name": "Product A", "Price": 10.00 },
    { "Name": "Product B", "Price": 20.00 }
  ]
}
```

**C# Code**:
```csharp
using TriasDev.Templify.Core;
using TriasDev.Templify.Utilities;

string json = File.ReadAllText("data.json");
var processor = new DocumentTemplateProcessor();

// Directly
ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, json);

// Or parse once into a dictionary (nested objects become dictionaries, arrays become lists)
Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);
```

Don't use `JsonSerializer.Deserialize<Dictionary<string, object>>`: it leaves the values as `JsonElement`s.
Since 1.8.0 nested paths such as `{{Customer.Name}}` resolve through them, but a top-level array fails in
`{{#foreach Items}}` ("is not a collection") and a JSON `false` counts as true in `{{#if}}`.

JSON is particularly useful for:
- Business users providing data
- API integrations
- Configuration files
- Testing with sample data

### Q: How do boolean expressions work in placeholders?

**A:** Use parentheses to evaluate logic directly in placeholders:

**Template**:
```
Eligible: {{(Age >= 18):yesno}}
Access granted: {{(IsActive and IsVerified):checkbox}}
Can proceed: {{(HasPermissionA or HasPermissionB):checkmark}}
```

**Data**:
```json
{
  "Age": 25,
  "IsActive": true,
  "IsVerified": true,
  "HasPermissionA": false,
  "HasPermissionB": true
}
```

**Output**:
```
Eligible: Yes
Access granted: ☑
Can proceed: ✓
```

**Supported operators**:
- Logical: `and`, `or`, `not`
- Comparison: `=`, `==`, `!=`, `>`, `>=`, `<`, `<=`
- Membership: `in` (collection variable, list literal, or comma-separated string)
- String checks: `contains`, `startswith`, `endswith` (case-sensitive)
- Existence: `exists`, `is empty`, `is not empty`
- Nested: `((var1 or var2) and var3)`

Inline expressions are stricter than `{{#if}}`: only a boolean `true` counts as true (a string `"true"` or the
number `1` do not), and single quotes are allowed for strings (`{{(Status = 'Active')}}`). An expression that
cannot be parsed stays in the output and adds an `ExpressionFailed` warning.

See the [Boolean Expressions Guide](for-template-authors/boolean-expressions.md) for complete documentation.

### Q: Can I combine expressions with format specifiers?

**A:** **Yes!** This is one of the most powerful features:

**Template**:
```
Qualified driver: {{((Age >= 18) and (HasLicense or HasPermit)):yesno}}
```

**Data**:
```json
{
  "Age": 20,
  "HasLicense": false,
  "HasPermit": true
}
```

**Output**:
```
Qualified driver: Yes
```

**More examples**:
```
Over budget: {{(Spent > Budget):checkbox}}
Status OK: {{(not HasErrors):checkmark}}
Premium member: {{(MembershipLevel >= 3):enabled}}
```

### Q: How do I customize boolean formatters?

**A:** Register custom formatters with the registry:

```csharp
using TriasDev.Templify.Core;
using TriasDev.Templify.Formatting;

var registry = new BooleanFormatterRegistry();
registry.Register("thumbs", new BooleanFormatter("👍", "👎"));
registry.Register("traffic", new BooleanFormatter("🟢", "🔴"));

var options = new PlaceholderReplacementOptions
{
    BooleanFormatterRegistry = registry
};
var processor = new DocumentTemplateProcessor(options);
```

**Template**:
```
User feedback: {{IsPositive:thumbs}}
System status: {{IsOperational:traffic}}
```

**Output**:
```
User feedback: 👍
System status: 🟢
```

### Q: Do format specifiers work with localization?

**A:** **Yes!** Format specifiers automatically adapt to the culture:

**German Output**:
```csharp
var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE")   // the built-in boolean formats follow Culture
};
var processor = new DocumentTemplateProcessor(options);
```

**Template**: `{{IsActive:yesno}}`
**Output (de-DE)**: `Ja` or `Nein`
**Output (fr-FR)**: `Oui` or `Non`
**Output (es-ES)**: `Sí` or `No`

**Supported languages for yesno**:
- English (en): Yes/No
- German (de): Ja/Nein
- French (fr): Oui/Non
- Spanish (es): Sí/No
- Italian (it): Sì/No
- Portuguese (pt): Sim/Não
- Dutch (nl): Ja/Nee
- Polish (pl): Tak/Nie
- Russian (ru): Да/Нет
- Japanese (ja): はい/いいえ
- Chinese (zh): 是/否

`truefalse`, `onoff`, `enabled` and `active` are localized for en, de, fr, es, it, pt, nl, pl and ru (English
otherwise). Symbol-based formatters (`checkbox`, `checkmark` and its alias `check`) are universal. Pass a
`BooleanFormatterRegistry` only when you need custom formatters.

---

## Performance

### Q: How fast is Templify?

**A:** Typical templates are processed in milliseconds; processing time grows linearly with document and data
size. See the [performance notes](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/PERFORMANCE.md) for a benchmark snapshot, and run
`TriasDev.Templify.Benchmarks` for numbers on your hardware.

### Q: Does Templify cache templates?

**A:** Templates are not cached (parsed condition expressions are). **Best practice**:
- For repeated processing, reuse the `DocumentTemplateProcessor` instance
- Keep the template bytes in memory and use `ProcessTemplate(byte[] template, data, out byte[] output)`
- The API is synchronous; process independent documents in parallel for throughput

### Q: Can I process templates in parallel?

**A:** **Yes!** A `DocumentTemplateProcessor` and its options can be shared by concurrent calls (register custom boolean formatters before sharing the options). Best practice:

```csharp
var processor = new DocumentTemplateProcessor();

Parallel.ForEach(dataList, data =>
{
    using var templateStream = GetTemplateStream(); // Separate stream per thread
    using var outputStream = GetOutputStream();

    processor.ProcessTemplate(templateStream, outputStream, data);
});
```

**Important**: Each thread needs its own template/output streams.

### Q: How can I optimize performance?

**A:** Tips:
1. **Reuse processor**: Create once, use many times
2. **Minimize template complexity**: Fewer loops = faster
3. **Pre-calculate values** in your code instead of complex template logic
4. **Use memory streams or byte arrays**: Faster than file I/O
5. **Batch processing**: Process multiple templates in parallel

---

## Troubleshooting

### Q: Why aren't my placeholders being replaced?

**A:** Common causes:
1. **Typo in placeholder name**: `{{Nmae}}` vs `{{Name}}` (dictionary keys are case-sensitive!)
2. **Data not provided**: Check `result.MissingVariables` and `result.Warnings`
3. **Wrong data structure**: Verify nested paths match your object
4. **Invalid placeholder syntax**: spaces inside the braces (`{{ Name }}`) or unsupported characters in the name are not recognized as placeholders

### Q: Word splits my placeholder into several runs. Is that a problem?

**A:** No. Word often splits text into runs (spell checking, formatting changes, revisions). Templify processes
the text of a paragraph as a whole, so `{{Name}}` is found even when it spans several runs. The replacement takes
the formatting of the run where the placeholder starts.

### Q: My conditional isn't working. What's wrong?

**A:** Check:
1. **Condition syntax**: `{{#if IsActive}}` not `{{if IsActive}}`
2. **Variable exists**: Provide the variable in data
3. **Truthiness**: `false`, `0`, `""`, `"0"`, `"false"`, `null` and empty collections are false; other values are true
4. **Operators**: Use `=` or `==` for equality and `and`/`or`/`not`; `&&`, `||` and `===` make the condition malformed (it evaluates to false and adds an `ExpressionFailed` warning)
5. **Precedence**: `not` applies to the whole comparison: `not Status = "Active"` means `not (Status = "Active")`
6. **Quotes**: Quote text values (`Status = "Active"`). An unquoted word is used as text only when no variable of that name exists
7. **Closing tag**: Must have `{{/if}}`; a missing one fails processing (`IsSuccess == false`)

### Q: Loop isn't repeating. Why?

**A:** Checklist:
1. **Collection exists**: Verify data contains the collection (`MissingLoopCollection` / `NullLoopCollection` warnings)
2. **Collection is enumerable**: Use `List<T>`, `T[]`, or `IEnumerable<T>`; a string or other scalar fails processing ("is not a collection")
3. **Markers in their own paragraphs** (or table rows): a paragraph with a marker is removed entirely
4. **Closing tag**: Must have `{{/foreach}}`
5. **Correct variable name**: Case-sensitive!

### Q: Why is my document corrupted after processing?

**A:** Common causes:
1. **Output not flushed**: Dispose (or flush) the output stream before reading the file; use `using` statements
2. **Reused output stream**: Use a new, empty output stream for each document (the template is copied into it)
3. **Concurrent access**: Don't share streams between threads
4. **Template already corrupted**: Validate the template with the [Converter tool](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify.Converter/README.md)

**Validate template**:
```bash
dotnet run --project TriasDev.Templify.Converter -- validate template.docx
```

### Q: How do I debug template issues?

**A:** Steps:
1. **Check `ProcessingResult`**:
   ```csharp
   if (!result.IsSuccess)
   {
       Console.WriteLine(result.ErrorMessage);
   }

   foreach (ProcessingWarning warning in result.Warnings)
   {
       Console.WriteLine(warning);
   }
   ```

2. **Validate the template** against your data:
   ```csharp
   ValidationResult validation = processor.ValidateTemplate(templateStream, data);
   // validation.Errors, validation.MissingVariables, validation.AllPlaceholders, validation.Warnings
   ```

3. **Simplify template**: Remove complexity until it works, then add back

4. **Write a warning report**: `File.WriteAllBytes("warnings.docx", result.GetWarningReportBytes());`

### Q: Can I see what Templify found in my template?

**A:** Yes! `ValidateTemplate` returns all placeholders and condition variables:

```csharp
using var template = File.OpenRead("template.docx");
ValidationResult validation = processor.ValidateTemplate(template);
Console.WriteLine(string.Join(", ", validation.AllPlaceholders));
```

The Converter's `analyze` command reports OpenXMLTemplates content controls (for migration), not Templify
placeholders.

---

## Migration

### Q: I'm using OpenXMLTemplates. How do I migrate?

**A:** Templify has a built-in converter!

**Step 1**: Analyze your template:
```bash
./scripts/analyze.sh old-template.docx
```

**Step 2**: Convert:
```bash
./scripts/convert.sh old-template.docx
```

**Step 3**: Process the converted template with Templify:
```csharp
var processor = new DocumentTemplateProcessor();
var data = new Dictionary<string, object> { ["Name"] = "John" };
ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);
```

`convert` only converts OpenXMLTemplates content controls; other content controls are kept unless you pass
`--unwrap-all-controls`. The CLI exits with 0 on success, 1 when the command failed (for example a control that
cannot be converted) and 2 for invalid arguments. See the full
[Converter Documentation](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify.Converter/README.md).

### Q: What's the mapping from OpenXMLTemplates?

| OpenXMLTemplates | Templify |
|------------------|----------|
| `variable_Name` | `{{Name}}` |
| `conditionalRemove_IsActive` | `{{#if IsActive}}...{{/if}}` |
| `conditionalRemove_Count_gt_0` | `{{#if Count > 0}}...{{/if}}` |
| `repeating_Items` | `{{#foreach Items}}...{{/foreach}}` |
| `variable_index` (inside a repeating section) | `{{@number}}` |

### Q: Can I migrate from manual OpenXML code?

**A:** **Yes!** Benefits:
- **90% less code**: Typical reduction
- **No XML knowledge needed**: Work with Word templates
- **Easier maintenance**: Templates updated by non-developers
- **Fewer bugs**: No manual XML manipulation

**Before** (manual OpenXML):
```csharp
// 50+ lines of XML manipulation
using (var doc = WordprocessingDocument.Open(...))
{
    var body = doc.MainDocumentPart.Document.Body;
    foreach (var text in body.Descendants<Text>())
    {
        if (text.Text.Contains("{{Name}}"))
        {
            text.Text = text.Text.Replace("{{Name}}", name);
        }
    }
    // ... many more lines
}
```

**After** (Templify):
```csharp
// 2 lines!
var data = new Dictionary<string, object> { ["Name"] = name };
processor.ProcessTemplate(templateStream, outputStream, data);
```

---

## Enterprise & Production

### Q: Is Templify production-ready?

**A:** **Yes!** Templify is battle-tested in production environments, processing thousands of documents daily.

**Quality indicators**:
- ✅ Extensive automated test suite, run on Windows, macOS and Linux in CI
- ✅ Public API guarded by API analyzers and package validation
- ✅ Consistent error model: template and data errors are returned as a failed `ProcessingResult`
- ✅ Well-documented

### Q: What about security?

**A:** Templify is designed with security in mind:
- ✅ **No code execution**: Templates are data, not code
- ✅ **No external dependencies**: Only OpenXML SDK
- ✅ **Template validation**: `ValidateTemplate` checks template structure before processing
- ✅ **No macro execution**: Security by design
- ✅ **Invalid XML characters** in data values are removed before they are written to the document

**Best practices**:
- Validate user-uploaded templates before processing
- Sanitize user input before passing to templates
- Set resource limits (size, timeouts) for user-uploaded templates and large-scale processing
- Keep OpenXML SDK updated

### Q: What's the license?

**A:** MIT. See the [LICENSE](https://github.com/TriasDev/templify/blob/main/LICENSE) file.

### Q: Is there commercial support available?

**A:** Templify is maintained by TriasDev GmbH & Co. KG. Support channels:
- 💬 Community support: [GitHub Discussions](https://github.com/TriasDev/templify/discussions)
- 🐛 Bug reports: [GitHub Issues](https://github.com/TriasDev/templify/issues)

### Q: Can I use Templify in commercial applications?

**A:** **Yes!** The MIT license allows commercial use.

### Q: How do I handle errors in production?

**A:** Robust error handling:

```csharp
try
{
    var result = processor.ProcessTemplate(templateStream, outputStream, data);

    if (!result.IsSuccess)
    {
        // Template syntax or data error
        _logger.LogError("Template processing failed: {Error}", result.ErrorMessage);

        // Optionally notify user
        return BadRequest("Document generation failed");
    }

    if (result.MissingVariables.Any())
    {
        // Log warnings
        _logger.LogWarning("Missing template variables: {Variables}",
            string.Join(", ", result.MissingVariables));
    }

    return File(outputStream.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
}
catch (InvalidOperationException ex)
{
    // Thrown for missing variables when MissingVariableBehavior.ThrowException is configured
    _logger.LogError(ex, "Missing template variable");
    return BadRequest("Incomplete data");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error processing template");
    return StatusCode(500, "Internal server error");
}
```

### Q: How do I monitor performance in production?

**A:** Track these metrics:

```csharp
var sw = Stopwatch.StartNew();
var result = processor.ProcessTemplate(templateStream, outputStream, data);
sw.Stop();

_logger.LogInformation(
    "Template processed: {Duration}ms, Placeholders: {Count}, Size: {Size}KB",
    sw.ElapsedMilliseconds,
    result.ReplacementCount,
    outputStream.Length / 1024
);

// Track in your monitoring system (Application Insights, Prometheus, etc.)
_metrics.RecordDuration("templify.processing", sw.ElapsedMilliseconds);
_metrics.RecordCount("templify.placeholders", result.ReplacementCount);
_metrics.RecordCount("templify.warnings", result.Warnings.Count);
```

---

## Comparison

### Q: Why use Templify instead of manual OpenXML?

**A:** Advantages:

| Aspect | Manual OpenXML | Templify |
|--------|----------------|----------|
| **Code complexity** | High (50-200 lines) | Low (5-10 lines) |
| **Learning curve** | Steep (XML knowledge) | Gentle (just placeholders) |
| **Template creation** | Programmatic | Visual (in Word) |
| **Maintenance** | Difficult | Easy |
| **Non-developer friendly** | No | Yes |
| **Error-prone** | High | Low |
| **Performance** | Similar | Similar |

### Q: Templify vs DocX library?

**A:**

| Feature | Templify | DocX |
|---------|----------|------|
| **Focus** | Template processing | Document creation |
| **Use case** | Fill templates | Build docs from scratch |
| **Conditionals** | ✅ Built-in | ❌ Code only |
| **Loops** | ✅ Built-in | ❌ Code only |
| **Template syntax** | ✅ Simple `{{}}` | ❌ N/A |
| **Learning curve** | Low | Medium |

**Choose Templify when**: You have Word templates to fill
**Choose DocX when**: You're building documents programmatically from scratch

### Q: Templify vs XSLT templating?

**A:**

| Aspect | Templify | XSLT |
|--------|----------|------|
| **Template format** | Word .docx | XML |
| **Readability** | High (visual) | Low (code-like) |
| **Designer-friendly** | Yes | No |
| **Complexity** | Simple | Complex |
| **Performance** | Fast | Slower |
| **Ecosystem** | .NET | Cross-platform |

### Q: When should I NOT use Templify?

**A:** Use alternatives when:
- **Generating PDFs directly**: Use PDF library (e.g., iText)
- **Complex layouts from scratch**: Use DocX or direct OpenXML
- **Real-time collaborative editing**: Use Office Online
- **Very simple text substitution**: Use `string.Replace()`
- **Excel files**: Use EPPlus or ClosedXML
- **Non-.NET environment**: Use platform-specific solutions

### Q: Can Templify replace reporting tools like Crystal Reports?

**A:** Partially. Comparison:

| Feature | Templify | Crystal Reports |
|---------|----------|-----------------|
| **Data binding** | ✅ Manual | ✅ Automatic |
| **Designer** | ✅ Word | ✅ Proprietary |
| **Conditionals** | ✅ Yes | ✅ Yes |
| **Loops** | ✅ Yes | ✅ Yes |
| **Charts/Graphs** | ❌ No | ✅ Yes |
| **Grouping** | ⚠️ Manual | ✅ Automatic |
| **Export formats** | Word only | Multiple |
| **Cost** | Free/Open-source | Commercial |

**Use Templify for**: Document-centric reports (contracts, letters, proposals)
**Use Crystal Reports for**: Data-heavy reports with charts and complex grouping

---

## Still Have Questions?

### Community Support
- 💬 [GitHub Discussions](https://github.com/TriasDev/templify/discussions) - Ask the community
- 🐛 [GitHub Issues](https://github.com/TriasDev/templify/issues) - Report bugs
- 📖 [Library README](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/README.md) - Complete reference

### Documentation
- [Quick Start Guide](for-developers/quick-start.md)
- [Tutorial Series](tutorials/index.md)
- [Architecture Guide](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/ARCHITECTURE.md)
- [Examples Collection](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/Examples.md)

### Can't Find Your Answer?
[Open a discussion](https://github.com/TriasDev/templify/discussions/new) or [create an issue](https://github.com/TriasDev/templify/issues/new) on GitHub.

