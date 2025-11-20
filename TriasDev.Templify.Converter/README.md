# Templify Converter

> CLI tool for converting OpenXMLTemplates documents to Templify format

## Overview

The Templify Converter is a command-line tool designed to help migrate Word document templates from the legacy **OpenXMLTemplates** format (content controls-based) to the modern **Templify** format (placeholder-based). It provides analysis, conversion, validation, and cleanup capabilities for Word documents.

**Key Features:**
- 🔍 **Analyze** - Inspect OpenXMLTemplates documents and identify content controls
- 🔄 **Convert** - Automatically migrate templates to Templify syntax
- ✅ **Validate** - Check document structure and schema validity
- 🧹 **Clean** - Remove Structured Document Tag (SDT) wrappers

## Prerequisites

- **.NET 9.0 SDK** or later
- Repository cloned locally

## Quick Start

### Basic Conversion Workflow

```bash
# 1. Analyze your template to understand its structure
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- analyze my-template.docx

# 2. Review the generated analysis report
cat my-template-analysis-report.md

# 3. Convert the template to Templify format
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- convert my-template.docx

# 4. Validate the converted document
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- validate my-template-templify.docx
```

### Using Helper Scripts (Recommended)

For easier usage, helper scripts are available in the `scripts/` directory:

```bash
# Make scripts executable (first time only, macOS/Linux)
chmod +x scripts/*.sh

# Use shortened commands
./scripts/analyze.sh my-template.docx
./scripts/convert.sh my-template.docx
./scripts/validate.sh my-template-templify.docx
```

See [scripts/README.md](../scripts/README.md) for complete script documentation.

## Commands Reference

### 📊 analyze - Template Analysis

Analyzes an OpenXMLTemplates document and generates a detailed report about content controls, their types, and suggested Templify conversions.

**Syntax:**
```bash
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- analyze <template-path> [options]
```

**Parameters:**
- `<template-path>` (required) - Path to the Word template file to analyze
- `--output <path>` or `-o <path>` (optional) - Custom path for the analysis report
  - Default: `<template-name>-analysis-report.md` in the same directory as the template

**Output:**
- Console summary with statistics
- Markdown report file with detailed analysis
- Control counts by type
- List of unique variable paths
- Suggested Templify syntax for each control
- Warnings for controls requiring manual review

**Example:**
```bash
# Analyze with default output location
./scripts/analyze.sh templates/invoice-template.docx

# Analyze with custom output path
./scripts/analyze.sh templates/invoice-template.docx --output reports/invoice-analysis.md
```

**Analysis Report Contents:**
- **Summary** - Total controls, types distribution
- **Variable Controls** - Simple placeholder replacements
- **Conditional Controls** - If/else logic with operators
- **Repeating Controls** - Collection loops
- **Warnings** - Complex controls needing manual attention
- **Unique Variables** - All variable paths found

**What Gets Analyzed:**
- Content controls (Structured Document Tags)
- Control types: Variable, Conditional, Repeating
- Tag formats:
  - `variable_variablePath` → `{{variablePath}}`
  - `conditionalRemove_condition` → `{{#if condition}}...{{/if}}`
  - `repeating_collectionPath` → `{{#foreach collectionPath}}...{{/foreach}}`
- Nested controls (tables, rows, inline)
- Complex conditionals with operators (eq, ne, gt, lt, gte, lte, and, or, not)

---

### 🔄 convert - Template Conversion

Converts an OpenXMLTemplates document to Templify format by replacing content control tags with Templify placeholders.

**Syntax:**
```bash
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- convert <template-path> [options]
```

**Parameters:**
- `<template-path>` (required) - Path to the Word template file to convert
- `--output <path>` or `-o <path>` (optional) - Custom path for the converted template
  - Default: `<template-name>-templify.docx` in the same directory

**Output:**
- Converted Word document with Templify syntax
- Console summary with conversion statistics
- Markdown conversion report
- Warnings for any failed conversions
- Count of removed SDT elements

**Example:**
```bash
# Convert with default output location
./scripts/convert.sh templates/invoice-template.docx

# Convert with custom output path
./scripts/convert.sh templates/invoice-template.docx --output output/invoice-new.docx
```

**Conversion Mappings:**

| OpenXMLTemplates Tag | Templify Syntax |
|---------------------|-----------------|
| `variable_CompanyName` | `{{CompanyName}}` |
| `variable_Customer.Address.City` | `{{Customer.Address.City}}` |
| `conditionalRemove_IsActive` | `{{#if IsActive}}...{{/if}}` |
| `conditionalRemove_Status_eq_Active` | `{{#if Status = "Active"}}...{{/if}}` |
| `conditionalRemove_Count_gt_0` | `{{#if Count > 0}}...{{/if}}` |
| `repeating_LineItems` | `{{#foreach LineItems}}...{{/foreach}}` |

**What Gets Converted:**
- Variable controls → Simple placeholders
- Conditional controls → If/else blocks (preserves else branches)
- Repeating controls → Foreach loops
- Complex conditionals → Templify conditional expressions
- All formatting preserved
- SDT wrappers removed

**Conversion Report:**
- Summary statistics
- Controls converted by type
- Warnings for manual review
- List of affected variables/collections

---

### ✅ validate - Document Validation

Validates that a Word document is well-formed and can be opened successfully.

**Syntax:**
```bash
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- validate <document-path>
```

**Parameters:**
- `<document-path>` (required) - Path to the Word document to validate

**Output:**
- Console validation results
- Detailed error messages for schema violations
- Exit code 0 if valid, 1 if invalid

**Example:**
```bash
# Validate a converted template
./scripts/validate.sh output/invoice-templify.docx
```

**What Gets Validated:**
- Document structure (MainDocumentPart, Body)
- OpenXML schema compliance
- Element relationships
- Required document parts
- Schema errors (up to 20 shown)

**Use Cases:**
- Verify conversion succeeded
- Check document integrity before distribution
- Troubleshoot corrupted templates
- Ensure document can be processed by Templify

---

### 🧹 clean - Document Cleanup

Removes all Structured Document Tag (SDT) elements from a document while preserving content.

**Syntax:**
```bash
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- clean <document-path> [options]
```

**Parameters:**
- `<document-path>` (required) - Path to the Word document to clean
- `--output <path>` or `-o <path>` (optional) - Path for the cleaned document
  - Default: Overwrites the input file (in-place cleaning)

**Output:**
- Cleaned Word document with SDT wrappers removed
- Console message with count of removed elements

**Example:**
```bash
# Clean in-place (overwrites original)
./scripts/clean.sh templates/old-template.docx

# Clean to new file
./scripts/clean.sh templates/old-template.docx --output templates/cleaned-template.docx
```

**Use Cases:**
- Fix documents with corrupted content controls
- Remove OpenXML control structures after conversion
- Prepare documents for manual placeholder insertion
- Clean up partially converted templates

**Warning:** In-place cleaning modifies the original file. Create a backup first or use `--output` to preserve the original.

---

### ❓ help - Help Information

Displays usage information and command descriptions.

**Syntax:**
```bash
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- help
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- --help
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- -h
```

**Output:**
- Command list with descriptions
- Basic usage examples
- Parameter syntax

---

## Migration Workflow Guide

### Step-by-Step: Migrating from OpenXMLTemplates

**1. Preparation**
```bash
# Create a backup of your templates
cp -r templates/ templates-backup/

# Ensure you're in the repository root
cd /path/to/templify
```

**2. Analysis Phase**
```bash
# Analyze each template to understand its structure
./scripts/analyze.sh templates/invoice.docx
./scripts/analyze.sh templates/report.docx
./scripts/analyze.sh templates/letter.docx

# Review the generated reports
cat templates/invoice-analysis-report.md
cat templates/report-analysis-report.md
cat templates/letter-analysis-report.md
```

**What to look for in analysis reports:**
- Controls marked "Requires Manual Review"
- Complex conditionals with multiple operators
- Nested repeating sections
- Unusual variable paths

**3. Conversion Phase**
```bash
# Convert templates one by one
./scripts/convert.sh templates/invoice.docx
./scripts/convert.sh templates/report.docx
./scripts/convert.sh templates/letter.docx

# Review conversion reports
cat templates/invoice-templify-conversion-report.md
cat templates/report-templify-conversion-report.md
cat templates/letter-templify-conversion-report.md
```

**4. Validation Phase**
```bash
# Validate converted documents
./scripts/validate.sh templates/invoice-templify.docx
./scripts/validate.sh templates/report-templify.docx
./scripts/validate.sh templates/letter-templify.docx
```

**5. Testing Phase**
```bash
# Test with actual data using Templify library
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj

# Or create a custom test program
dotnet new console -n TemplifyTest
# Add reference and test code
```

**6. Manual Review**
- Open converted templates in Word
- Check that all placeholders are correct
- Verify conditional blocks are properly structured
- Test with sample data
- Fix any issues flagged in conversion reports

**7. Deployment**
- Replace old templates with converted versions
- Update application code to use Templify API
- Deploy and test in staging environment
- Monitor for issues

---

## Understanding OpenXMLTemplates vs Templify

### Format Comparison

**OpenXMLTemplates (Content Controls)**
- Uses Structured Document Tags (SDTs)
- Tags embedded as XML attributes
- Format: `variable_variablePath`, `conditionalRemove_condition`, `repeating_collection`
- Requires special Word setup to create controls
- Not human-readable in Word

**Templify (Text Placeholders)**
- Uses simple text placeholders
- Plain text in the document
- Format: `{{variableName}}`, `{{#if condition}}`, `{{#foreach collection}}`
- Type directly in Word - no special controls needed
- Human-readable and intuitive

### Migration Benefits

✅ **Simpler template creation** - No content controls required
✅ **Better designer experience** - Type placeholders like regular text
✅ **More maintainable** - Templates are easier to read and edit
✅ **Fewer Word issues** - No SDT corruption problems
✅ **Modern architecture** - Visitor pattern, better extensibility
✅ **Better performance** - Optimized processing

### Tag Format Examples

#### Variables
```
OpenXMLTemplates: <SDT tag="variable_CompanyName">
Templify:        {{CompanyName}}
```

#### Nested Variables
```
OpenXMLTemplates: <SDT tag="variable_Customer.Address.City">
Templify:        {{Customer.Address.City}}
```

#### Conditionals
```
OpenXMLTemplates: <SDT tag="conditionalRemove_IsActive">Content</SDT>
Templify:        {{#if IsActive}}Content{{/if}}
```

#### Conditional with Operator
```
OpenXMLTemplates: <SDT tag="conditionalRemove_Status_eq_Active">
Templify:        {{#if Status = "Active"}}...{{/if}}
```

#### Loops
```
OpenXMLTemplates: <SDT tag="repeating_LineItems">Item content</SDT>
Templify:        {{#foreach LineItems}}Item content{{/foreach}}
```

---

## Examples

### Example 1: Basic Conversion

```bash
# You have an OpenXMLTemplates invoice template
./scripts/analyze.sh invoices/invoice-template.docx

# Review shows 15 variables, 3 conditionals, 1 repeating section
# Convert it
./scripts/convert.sh invoices/invoice-template.docx

# Output: invoices/invoice-template-templify.docx
# Validate the result
./scripts/validate.sh invoices/invoice-template-templify.docx

# ✓ Document is valid
```

### Example 2: Batch Processing Multiple Templates

```bash
# Process all templates in a directory
for template in templates/*.docx; do
  echo "Processing: $template"
  ./scripts/analyze.sh "$template"
  ./scripts/convert.sh "$template"
  ./scripts/validate.sh "${template%.docx}-templify.docx"
  echo "---"
done
```

### Example 3: Custom Output Directory

```bash
# Organize converted templates in a separate directory
mkdir -p output/converted

for template in templates/*.docx; do
  basename=$(basename "$template" .docx)
  ./scripts/convert.sh "$template" --output "output/converted/${basename}-new.docx"
done
```

### Example 4: Complete Migration Scenario

```bash
# You're migrating 20 invoice templates
cd /path/to/templify

# Step 1: Bulk analyze
mkdir -p migration-reports
for template in old-templates/*.docx; do
  basename=$(basename "$template" .docx)
  ./scripts/analyze.sh "$template" --output "migration-reports/${basename}-analysis.md"
done

# Step 2: Review all analysis reports
cat migration-reports/*-analysis.md | grep "Requires Manual Review"

# Step 3: Bulk convert
mkdir -p new-templates
for template in old-templates/*.docx; do
  basename=$(basename "$template" .docx)
  ./scripts/convert.sh "$template" --output "new-templates/${basename}.docx"
done

# Step 4: Bulk validate
for template in new-templates/*.docx; do
  ./scripts/validate.sh "$template" || echo "FAILED: $template"
done

# Step 5: Test with Templify
# Use demo or custom code to process with actual data
```

### Example 5: Handling Conversion Warnings

```bash
# Convert and capture warnings
./scripts/convert.sh complex-template.docx 2>&1 | tee conversion.log

# Review conversion report for issues
cat complex-template-templify-conversion-report.md

# If warnings exist, manually fix in Word
# Then validate again
./scripts/validate.sh complex-template-templify.docx
```

---

## Troubleshooting

### Issue: "File not found" Error

**Cause:** Incorrect path to template file

**Solution:**
```bash
# Use absolute path
./scripts/analyze.sh /full/path/to/template.docx

# Or navigate to the correct directory
cd templates/
../scripts/analyze.sh invoice.docx
```

### Issue: Conversion Produces Warnings

**Cause:** Complex or nested content controls that need manual review

**Solution:**
1. Review the conversion report (`*-conversion-report.md`)
2. Check the analysis report for "Requires Manual Review" sections
3. Open the converted document in Word
4. Manually adjust problem areas
5. Validate again

### Issue: Converted Document Won't Open in Word

**Cause:** Document corruption during conversion

**Solution:**
```bash
# Validate to see specific errors
./scripts/validate.sh converted-template.docx

# If validation fails, try cleaning the original first
./scripts/clean.sh original-template.docx --output cleaned-original.docx
./scripts/convert.sh cleaned-original.docx
```

### Issue: Script Permission Denied (macOS/Linux)

**Cause:** Scripts not executable

**Solution:**
```bash
# Make all scripts executable
chmod +x scripts/*.sh

# Or make individual script executable
chmod +x scripts/convert.sh
```

### Issue: Missing Variables After Conversion

**Cause:** Variable controls not properly detected or converted

**Solution:**
1. Check the analysis report to see what was detected
2. Verify the OpenXMLTemplates tag format is correct
3. Manually add missing placeholders in Word after conversion

### Issue: Complex Conditionals Not Converting

**Cause:** Operator syntax not recognized or too complex

**Solution:**
1. Review the specific conditional in the analysis report
2. Check if operator is supported (eq, ne, gt, lt, gte, lte, and, or, not)
3. Manually create the conditional in Templify syntax after conversion
4. Refer to [Templify conditional documentation](../TriasDev.Templify/README.md#conditional-blocks)

### Issue: Nested Loops Not Working

**Cause:** Nested repeating controls may have complex structure

**Solution:**
1. Check the conversion report for warnings about nested controls
2. Manually verify loop markers are correctly placed
3. Test with actual data to ensure proper iteration

---

## Technical Details

### Supported OpenXMLTemplates Tag Formats

**Variable Tags:**
- `variable_SimpleName` → `{{SimpleName}}`
- `variable_Nested.Path.Name` → `{{Nested.Path.Name}}`
- `variable_Array[0].Property` → `{{Array[0].Property}}`

**Conditional Tags:**
- `conditionalRemove_BoolVar` → `{{#if BoolVar}}...{{/if}}`
- `conditionalRemove_Var_eq_Value` → `{{#if Var = "Value"}}...{{/if}}`
- `conditionalRemove_Count_gt_0` → `{{#if Count > 0}}...{{/if}}`
- `conditionalRemove_A_and_B` → `{{#if A and B}}...{{/if}}`
- `conditionalRemove_X_or_Y` → `{{#if X or Y}}...{{/if}}`

**Repeating Tags:**
- `repeating_CollectionName` → `{{#foreach CollectionName}}...{{/foreach}}`
- `repeating_Nested.Collection` → `{{#foreach Nested.Collection}}...{{/foreach}}`

### Operators Supported

- **Equality:** `eq` → `=`, `ne` → `!=`
- **Comparison:** `gt` → `>`, `lt` → `<`, `gte` → `>=`, `lte` → `<=`
- **Logical:** `and`, `or`, `not`

### Limitations

- **Complex expressions:** Very complex conditionals may require manual review
- **Custom operators:** Non-standard operators not supported
- **Nested conditionals:** Some deeply nested structures may need manual adjustment
- **SDT corruption:** Already corrupted documents may not convert cleanly

---

## Related Documentation

- 📖 **[Templify Library Documentation](../TriasDev.Templify/README.md)** - Full API reference and usage guide
- 🏗️ **[Architecture Documentation](../TriasDev.Templify/ARCHITECTURE.md)** - Technical design details
- 📝 **[Code Examples](../TriasDev.Templify/Examples.md)** - Templify usage examples
- 🤖 **[Development Guide](../CLAUDE.md)** - AI-assisted development guidance
- 📜 **[Script Documentation](../scripts/README.md)** - Helper script usage

---

## Support

For issues or questions:
- Review the [Troubleshooting](#troubleshooting) section
- Check conversion reports for specific warnings
- Consult the [Templify documentation](../TriasDev.Templify/README.md)
- Contact TriasDev internal support

---

**Part of TriasDev.Templify Project**
© TriasDev GmbH & Co. KG
