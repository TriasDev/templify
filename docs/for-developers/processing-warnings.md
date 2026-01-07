# Processing Warnings

Templify collects non-fatal warnings during template processing, helping you identify issues like missing variables or null collections without failing the entire operation.

## Accessing Warnings

Warnings are available on the `ProcessingResult` returned by `ProcessTemplate`:

```csharp
var processor = new DocumentTemplateProcessor();
var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.IsSuccess)
{
    Console.WriteLine($"Processed with {result.Warnings.Count} warnings");

    if (result.HasWarnings)
    {
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"  [{warning.Type}] {warning.VariableName}: {warning.Message}");
        }
    }
}
```

## Warning Types

| Type | Description | When Generated |
|------|-------------|----------------|
| `MissingVariable` | Variable not found in data | Placeholder like `{{CustomerName}}` when `CustomerName` is not in the data dictionary |
| `MissingLoopCollection` | Loop collection not found | `{{#foreach Items}}` when `Items` is not in the data dictionary |
| `NullLoopCollection` | Loop collection is null | `{{#foreach Items}}` when `Items` exists but is `null` |
| `ExpressionFailed` | Expression parsing or evaluation failed | `{{(Status = "Active")}}` with invalid syntax (should be `==`) |

## Warning Properties

Each `ProcessingWarning` contains:

```csharp
public class ProcessingWarning
{
    public ProcessingWarningType Type { get; }    // Warning category
    public string VariableName { get; }           // The variable/expression that caused the warning
    public string Context { get; }                // Where it occurred (e.g., "placeholder", "loop: Items")
    public string Message { get; }                // Human-readable description
}
```

## Generating Warning Reports

Templify can generate a Word document report of all warnings:

```csharp
var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.HasWarnings)
{
    // Get report as MemoryStream
    using var reportStream = result.GetWarningReport();

    // Or get report as byte array
    byte[] reportBytes = result.GetWarningReportBytes();
    File.WriteAllBytes("warnings.docx", reportBytes);
}
```

The generated report includes:

- Summary with total warning counts by type
- Detailed sections for each warning type
- Variable names, contexts, and error messages

## Example: Logging All Warnings

```csharp
var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.HasWarnings)
{
    _logger.LogWarning("Template processed with {Count} warnings", result.Warnings.Count);

    foreach (var warning in result.Warnings)
    {
        switch (warning.Type)
        {
            case ProcessingWarningType.MissingVariable:
                _logger.LogWarning("Missing variable: {Variable}", warning.VariableName);
                break;
            case ProcessingWarningType.MissingLoopCollection:
                _logger.LogWarning("Missing collection: {Collection}", warning.VariableName);
                break;
            case ProcessingWarningType.NullLoopCollection:
                _logger.LogWarning("Null collection: {Collection}", warning.VariableName);
                break;
            case ProcessingWarningType.ExpressionFailed:
                _logger.LogWarning("Expression failed: {Expression} - {Message}",
                    warning.VariableName, warning.Message);
                break;
        }
    }
}
```

## Behavior Notes

- **Empty collections** do not generate warnings (they're valid, just produce no output)
- **Valid expressions with missing variables** evaluate to `false` without warnings (e.g., `{{(Price > 100)}}` where `Price` is missing returns `false`)
- **Invalid expression syntax** generates `ExpressionFailed` (e.g., using `=` instead of `==`)
- Warnings are collected even when `MissingVariableBehavior` is set to `LeaveUnchanged` or `ReplaceWithEmpty`
