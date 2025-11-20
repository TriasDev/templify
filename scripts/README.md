# Templify Converter Helper Scripts

> Simplified command-line shortcuts for the Templify Converter CLI tool

## Overview

These helper scripts provide shortened commands for running the Templify Converter, reducing the verbose `dotnet run --project ...` invocations to simple script calls.

**Available Scripts:**
- `analyze.sh` / `analyze.cmd` - Analyze OpenXMLTemplates documents
- `convert.sh` / `convert.cmd` - Convert templates to Templify format
- `validate.sh` / `validate.cmd` - Validate Word document structure
- `clean.sh` / `clean.cmd` - Remove SDT elements from documents

## Setup

### macOS / Linux

Make the scripts executable (first time only):

```bash
chmod +x scripts/*.sh
```

### Windows

No setup required - `.cmd` files are executable by default.

## Usage

### macOS / Linux

Run scripts with `./scripts/script-name.sh`:

```bash
# From repository root
./scripts/analyze.sh template.docx
./scripts/convert.sh template.docx
./scripts/validate.sh template.docx
./scripts/clean.sh template.docx
```

### Windows

Run scripts with `scripts\script-name.cmd`:

```cmd
REM From repository root
scripts\analyze.cmd template.docx
scripts\convert.cmd template.docx
scripts\validate.cmd template.docx
scripts\clean.cmd template.docx
```

### From Any Directory

Add the scripts directory to your PATH or navigate to the repository root first:

```bash
# Navigate to repository
cd /path/to/templify

# Run script
./scripts/convert.sh my-template.docx
```

## Command Examples

### Analyze Command

**Basic usage:**
```bash
./scripts/analyze.sh templates/invoice.docx
```

**With custom output:**
```bash
./scripts/analyze.sh templates/invoice.docx --output reports/invoice-analysis.md
```

**Short form:**
```bash
./scripts/analyze.sh templates/invoice.docx -o reports/invoice-analysis.md
```

### Convert Command

**Basic usage:**
```bash
./scripts/convert.sh templates/invoice.docx
```

**With custom output:**
```bash
./scripts/convert.sh templates/invoice.docx --output output/invoice-new.docx
```

**Short form:**
```bash
./scripts/convert.sh templates/invoice.docx -o output/invoice-new.docx
```

### Validate Command

**Basic usage:**
```bash
./scripts/validate.sh templates/invoice-templify.docx
```

### Clean Command

**In-place cleaning (overwrites original):**
```bash
./scripts/clean.sh templates/old-template.docx
```

**With output to new file:**
```bash
./scripts/clean.sh templates/old-template.docx --output templates/cleaned-template.docx
```

## Batch Processing Examples

### Process All Templates in a Directory

**macOS / Linux:**
```bash
for template in templates/*.docx; do
  echo "Analyzing: $template"
  ./scripts/analyze.sh "$template"
done
```

**Windows:**
```cmd
for %%f in (templates\*.docx) do (
    echo Analyzing: %%f
    scripts\analyze.cmd "%%f"
)
```

### Full Conversion Workflow

**macOS / Linux:**
```bash
#!/bin/bash
# Convert all templates in a directory

for template in old-templates/*.docx; do
  basename=$(basename "$template" .docx)

  echo "Processing: $basename"

  # Analyze
  ./scripts/analyze.sh "$template" -o "reports/${basename}-analysis.md"

  # Convert
  ./scripts/convert.sh "$template" -o "new-templates/${basename}.docx"

  # Validate
  ./scripts/validate.sh "new-templates/${basename}.docx"

  echo "---"
done
```

**Windows:**
```cmd
@echo off
REM Convert all templates in a directory

for %%f in (old-templates\*.docx) do (
    set "basename=%%~nf"

    echo Processing: %%~nf

    REM Analyze
    scripts\analyze.cmd "%%f" -o "reports\%%~nf-analysis.md"

    REM Convert
    scripts\convert.cmd "%%f" -o "new-templates\%%~nf.docx"

    REM Validate
    scripts\validate.cmd "new-templates\%%~nf.docx"

    echo ---
)
```

## Comparison: With vs Without Scripts

### Without Scripts (Verbose)

```bash
# Long form - 80+ characters
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- analyze templates/invoice.docx --output reports/invoice-analysis.md
```

### With Scripts (Concise)

```bash
# Short form - ~70 characters saved
./scripts/analyze.sh templates/invoice.docx -o reports/invoice-analysis.md
```

**Benefits:**
- ✅ 70-80% shorter commands
- ✅ Faster to type
- ✅ Easier to remember
- ✅ Consistent across projects
- ✅ Works on both macOS/Linux and Windows

## Creating Aliases (Optional)

For even shorter commands, create shell aliases:

### macOS / Linux (bash/zsh)

Add to `~/.bashrc` or `~/.zshrc`:

```bash
alias tanalyze='cd /path/to/templify && ./scripts/analyze.sh'
alias tconvert='cd /path/to/templify && ./scripts/convert.sh'
alias tvalidate='cd /path/to/templify && ./scripts/validate.sh'
alias tclean='cd /path/to/templify && ./scripts/clean.sh'
```

Then use from anywhere:

```bash
tconvert ~/Documents/my-template.docx
tvalidate ~/Documents/my-template-templify.docx
```

### Windows (PowerShell)

Add to PowerShell profile (`$PROFILE`):

```powershell
function tanalyze {
    cd C:\path\to\templify
    scripts\analyze.cmd $args
}
function tconvert {
    cd C:\path\to\templify
    scripts\convert.cmd $args
}
function tvalidate {
    cd C:\path\to\templify
    scripts\validate.cmd $args
}
function tclean {
    cd C:\path\to\templify
    scripts\clean.cmd $args
}
```

Then use from anywhere:

```powershell
tconvert C:\Users\YourName\Documents\my-template.docx
tvalidate C:\Users\YourName\Documents\my-template-templify.docx
```

## Troubleshooting

### Issue: "Permission denied" (macOS/Linux)

**Solution:**
```bash
chmod +x scripts/*.sh
```

### Issue: "Command not found" (macOS/Linux)

**Cause:** Not running from repository root or using wrong path

**Solution:**
```bash
# Ensure you're in repository root
cd /path/to/templify

# Use ./ prefix
./scripts/analyze.sh template.docx
```

### Issue: Scripts not working (Windows)

**Cause:** Incorrect path separator

**Solution:**
```cmd
REM Use backslashes on Windows
scripts\analyze.cmd template.docx

REM Not forward slashes
scripts/analyze.cmd template.docx  REM Won't work
```

### Issue: ".NET SDK not found"

**Cause:** .NET 9.0 SDK not installed or not in PATH

**Solution:**
1. Install .NET 9.0 SDK from https://dotnet.microsoft.com/download
2. Verify installation: `dotnet --version`
3. Restart terminal/command prompt

### Issue: "Project not found"

**Cause:** Running scripts from wrong directory

**Solution:**
```bash
# Scripts must be run from repository root
cd /path/to/templify
./scripts/convert.sh template.docx
```

## Script Contents

Each script is a thin wrapper around the converter CLI:

**Bash scripts (`.sh`):**
```bash
#!/bin/bash
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- [command] "$@"
```

**Windows scripts (`.cmd`):**
```cmd
@echo off
dotnet run --project TriasDev.Templify.Converter\TriasDev.Templify.Converter.csproj -- [command] %*
```

All command-line arguments are passed through to the converter unchanged.

## Related Documentation

- 📖 **[Converter Documentation](../TriasDev.Templify.Converter/README.md)** - Full converter command reference
- 📚 **[Templify Library Documentation](../TriasDev.Templify/README.md)** - Templify API and usage
- 📝 **[Root README](../README.md)** - Repository overview

---

**Part of TriasDev.Templify Project**
© TriasDev GmbH & Co. KG
