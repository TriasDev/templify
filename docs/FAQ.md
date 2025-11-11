# Frequently Asked Questions (FAQ)

Common questions and answers about using Templify for Word document templating.

---

## Table of Contents

- [Getting Started](#getting-started)
- [Features & Capabilities](#features--capabilities)
- [Syntax & Usage](#syntax--usage)
- [Performance](#performance)
- [Troubleshooting](#troubleshooting)
- [Migration](#migration)
- [Enterprise & Production](#enterprise--production)
- [Comparison](#comparison)

---

## Getting Started

### Q: What is Templify?

**A:** Templify is a modern .NET library for generating Word documents from templates. You create a Word template with placeholders like `{{Name}}`, and Templify replaces them with your data. It supports conditionals, loops, and nested data structures without requiring Microsoft Word to be installed.

### Q: What are the system requirements?

**A:**
- **.NET 9.0 or later**
- **DocumentFormat.OpenXml 3.3.0** (automatically installed via NuGet)
- Any platform supported by .NET (Windows, macOS, Linux)
- **No Microsoft Word installation required**

### Q: How do I install Templify?

**A:** Via NuGet:
```bash
dotnet add package TriasDev.Templify
```

Or in Visual Studio: `Install-Package TriasDev.Templify`

### Q: Where should I start?

**A:** Follow this learning path:
1. [Quick Start Guide](quick-start.md) (5 minutes)
2. [Tutorial 1: Hello World](tutorials/01-hello-world.md) (30 min)
3. [Tutorial 2: Invoice Generator](tutorials/02-invoice-generator.md) (1 hour)
4. [Full API Documentation](../TriasDev.Templify/README.md)

### Q: Do I need Microsoft Word installed?

**A:** **No!** Templify uses the OpenXML SDK to work directly with .docx files. It runs on any platform without Office installed.

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
- ✅ **Conditionals**: `{{#if IsActive}}...{{else}}...{{/if}}`
- ✅ **Loops**: `{{#foreach Items}}...{{/foreach}}`
- ✅ **Nested loops**: Loops inside loops (arbitrary depth)
- ✅ **Table row loops**: Dynamic table generation
- ✅ **Loop variables**: `@index`, `@first`, `@last`, `@count`
- ✅ **Formatting preservation**: Bold, italic, colors, fonts maintained
- ✅ **Comparison operators**: `>`, `<`, `>=`, `<=`, `==`, `!=`
- ✅ **Logical operators**: `&&`, `||`
- ✅ **Type coercion**: Automatic number/date conversions

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
- Processes **1,000+ placeholders in ~100ms**
- Handles documents with **hundreds of pages**
- Low memory footprint
- See [Performance Benchmarks](../TriasDev.Templify/PERFORMANCE.md)

### Q: Does Templify support tables?

**A:** **Yes!** Full table support:
- Replace placeholders in table cells
- Loop over table rows: `{{#foreach Items}}`
- Nested tables
- Conditional rows
- Formatting preservation

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
- With spaces: `{{ Name }}` (spaces ignored)

**Rules**:
- Letters, numbers, underscore, dot, brackets only
- Case-sensitive
- No special characters in variable names

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

**A:** Use `{{#if}}...{{else}}...{{/if}}`:

```
{{#if IsActive}}
This customer is active.
{{else}}
This customer is inactive.
{{/if}}
```

**Supported conditions**:
- Boolean: `{{#if IsActive}}`
- Comparison: `{{#if Age > 18}}`
- Null check: `{{#if Email}}` (true if not null/empty)
- Combined: `{{#if Age >= 18 && HasLicense}}`

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

**Output**:
```
- Product A: 10.00 EUR
- Product B: 20.00 EUR
```

### Q: What are loop special variables?

**A:** Inside loops, use these:
- `{{@index}}` - Current position (0-based)
- `{{@first}}` - True for first item
- `{{@last}}` - True for last item
- `{{@count}}` - Total number of items

**Example**:
```
{{#foreach Items}}
Item {{@index}}: {{Name}}{{#if @last}} (last){{/if}}
{{/foreach}}
```

### Q: Can I nest loops?

**A:** **Yes!** Unlimited nesting:

```
{{#foreach Orders}}
Order #{{OrderId}}:
  {{#foreach Items}}
  - {{Product}}: {{Quantity}} x {{Price}}
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

Templify will repeat the row for each item.

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

Missing variables are left as-is: `{{MissingVar}}` remains in output.

---

## Performance

### Q: How fast is Templify?

**A:** Very fast! Benchmark results:
- **Simple replacement**: ~0.5ms for 10 placeholders
- **Complex document**: ~100ms for 1,000 placeholders
- **Table with loops**: ~50ms for 100 rows

See detailed [Performance Benchmarks](../TriasDev.Templify/PERFORMANCE.md).

### Q: Does Templify cache templates?

**A:** No internal caching. **Best practice**:
- For repeated processing, reuse the `DocumentTemplateProcessor` instance
- Cache template streams in your application if needed
- Process templates asynchronously for better throughput

### Q: Can I process templates in parallel?

**A:** **Yes!** `DocumentTemplateProcessor` is thread-safe for reading. Best practice:

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
3. **Pre-calculate values**: Don't use complex nested paths
4. **Use memory streams**: Faster than file I/O
5. **Batch processing**: Process multiple templates in parallel

---

## Troubleshooting

### Q: Why aren't my placeholders being replaced?

**A:** Common causes:
1. **Typo in placeholder name**: `{{Nmae}}` vs `{{Name}}` (case-sensitive!)
2. **Data not provided**: Check `result.MissingVariables`
3. **Wrong data structure**: Verify nested paths match your object
4. **Word formatting broke placeholder**: Word sometimes splits `{{Name}}` into multiple runs

**Fix #4**: Select the placeholder in Word and clear formatting (Ctrl+Space), then retype it.

### Q: Why do I get "Placeholder split across runs" warning?

**A:** Word's internal format sometimes splits text. **Fix**:
1. Select the placeholder in Word
2. Press **Ctrl+Space** (remove formatting)
3. Retype the placeholder without formatting changes mid-text

### Q: My conditional isn't working. What's wrong?

**A:** Check:
1. **Condition syntax**: `{{#if IsActive}}` not `{{if IsActive}}`
2. **Variable exists**: Provide the variable in data
3. **Type mismatch**: `"true"` (string) is not `true` (boolean)
4. **Comparison operators**: Use `==` for equality, not `=`
5. **Closing tag**: Must have `{{/if}}`

### Q: Loop isn't repeating. Why?

**A:** Checklist:
1. **Collection exists**: Verify data contains the collection
2. **Collection is enumerable**: Use `List<T>`, `T[]`, or `IEnumerable<T>`
3. **Closing tag**: Must have `{{/foreach}}`
4. **Correct variable name**: Case-sensitive!

### Q: Why is my document corrupted after processing?

**A:** Common causes:
1. **Stream not disposed**: Use `using` statements
2. **Stream position not reset**: Call `stream.Position = 0` before processing
3. **Concurrent access**: Don't share streams between threads
4. **Template already corrupted**: Validate template with [Converter tool](../TriasDev.Templify.Converter/README.md)

**Validate template**:
```bash
dotnet run --project TriasDev.Templify.Converter -- validate template.docx
```

### Q: How do I debug template issues?

**A:** Steps:
1. **Check `ProcessingResult`**:
   ```csharp
   if (!result.IsSuccessful)
   {
       foreach (var error in result.Errors)
           Console.WriteLine(error);
   }
   ```

2. **Review missing variables**:
   ```csharp
   foreach (var missing in result.MissingVariables)
       Console.WriteLine($"Missing: {missing}");
   ```

3. **Simplify template**: Remove complexity until it works, then add back

4. **Check template structure**: Use Converter's analyze command:
   ```bash
   dotnet run --project TriasDev.Templify.Converter -- analyze template.docx
   ```

### Q: Can I see what Templify found in my template?

**A:** Yes! Use the Converter tool:

```bash
dotnet run --project TriasDev.Templify.Converter -- analyze template.docx --output report.md
```

Shows all placeholders, conditionals, and loops found.

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

**Step 3**: Update code:
```csharp
// Old: OpenXMLTemplates
var processor = new TemplateProcessor("template.docx");
processor.SetValue("variable_Name", "John");

// New: Templify
var processor = new DocumentTemplateProcessor();
var data = new Dictionary<string, object> { ["Name"] = "John" };
processor.ProcessTemplate(templateStream, outputStream, data);
```

See full [Converter Documentation](../TriasDev.Templify.Converter/README.md).

### Q: What's the mapping from OpenXMLTemplates?

| OpenXMLTemplates | Templify |
|------------------|----------|
| `variable_Name` | `{{Name}}` |
| `conditionalRemove_IsActive` | `{{#if IsActive}}...{{/if}}` |
| `conditionalRemove_Count_gt_0` | `{{#if Count > 0}}...{{/if}}` |
| `repeating_Items` | `{{#foreach Items}}...{{/foreach}}` |

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
// 3 lines!
var data = new Dictionary<string, object> { ["Name"] = name };
processor.ProcessTemplate(templateStream, outputStream, data);
```

---

## Enterprise & Production

### Q: Is Templify production-ready?

**A:** **Yes!**  Currently used in production in [ViasPro](https://viaspro.com), processing thousands of compliance documents daily.

**Quality indicators**:
- ✅ 109 tests, 100% code coverage
- ✅ Battle-tested in enterprise environment
- ✅ Comprehensive error handling
- ✅ Performance optimized
- ✅ Well-documented

### Q: What about security?

**A:** Templify is designed with security in mind:
- ✅ **No code execution**: Templates are data, not code
- ✅ **No external dependencies**: Only OpenXML SDK
- ✅ **Input validation**: Validates template structure
- ✅ **No macro execution**: Security by design
- ✅ **Safe XML processing**: Protection against XXE attacks
- ✅ **Memory limits**: Protection against zip bombs

**Best practices**:
- Validate user-uploaded templates before processing
- Sanitize user input before passing to templates
- Set resource limits for large-scale processing
- Keep OpenXML SDK updated

### Q: What's the license?

**A:** *[License information to be added]* - See LICENSE file in repository.

### Q: Is there commercial support available?

**A:** Templify is maintained by TriasDev GmbH & Co. KG. For enterprise support:
- 📧 Contact: *[contact information to be added]*
- 💬 Community support: [GitHub Discussions](https://github.com/TriasDev/templify/discussions)
- 🐛 Bug reports: [GitHub Issues](https://github.com/TriasDev/templify/issues)

### Q: Can I use Templify in commercial applications?

**A:** **Yes!** Templify can be used in commercial applications. Check the LICENSE file for specific terms.

### Q: How do I handle errors in production?

**A:** Robust error handling:

```csharp
try
{
    var result = processor.ProcessTemplate(templateStream, outputStream, data);

    if (!result.IsSuccessful)
    {
        // Log errors for investigation
        _logger.LogError("Template processing failed: {Errors}",
            string.Join(", ", result.Errors));

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
    result.PlaceholdersReplaced,
    outputStream.Length / 1024
);

// Track in your monitoring system (Application Insights, Prometheus, etc.)
_metrics.RecordDuration("templify.processing", sw.ElapsedMilliseconds);
_metrics.RecordCount("templify.placeholders", result.PlaceholdersReplaced);
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
| **Performance** | Similar | Optimized |

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
- 📖 [Full Documentation](../TriasDev.Templify/README.md) - Complete reference

### Documentation
- [Quick Start Guide](quick-start.md)
- [Tutorial Series](tutorials/)
- [API Reference](../TriasDev.Templify/README.md)
- [Architecture Guide](../TriasDev.Templify/ARCHITECTURE.md)
- [Examples Collection](../TriasDev.Templify/Examples.md)

### Can't Find Your Answer?
[Open a discussion](https://github.com/TriasDev/templify/discussions/new) or [create an issue](https://github.com/TriasDev/templify/issues/new) on GitHub.

---

**Last Updated**: 2025-01-15
