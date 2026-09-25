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
# From repository root (or any other directory - see below)
./scripts/analyze.sh template.docx
./scripts/convert.sh template.docx
./scripts/validate.sh template.docx
./scripts/clean.sh template.docx
```

### Windows

Run scripts with `scripts\script-name.cmd`:

```cmd
REM From repository root (or any other directory - see below)
scripts\analyze.cmd template.docx
scripts\convert.cmd template.docx
scripts\validate.cmd template.docx
scripts\clean.cmd template.docx
```

### From Any Directory

The scripts locate the converter project relative to their own location (`$(dirname "$0")/..` in bash,
`%~dp0..` in cmd), so they work from any directory. File arguments are resolved relative to **your current
directory**, not the repository.

```bash
# Call the script by path from anywhere
cd ~/Documents/templates
/path/to/templify/scripts/convert.sh my-template.docx

# Or add the scripts directory to your PATH
export PATH="/path/to/templify/scripts:$PATH"
convert.sh my-template.docx
```

```cmd
REM Windows: add the scripts directory to PATH
set PATH=C:\path\to\templify\scripts;%PATH%
convert.cmd my-template.docx
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

**Also unwrap non-OpenXMLTemplates content controls (TOC, check boxes, ...):**
```bash
./scripts/convert.sh templates/invoice.docx --unwrap-all-controls
```

By default only OpenXMLTemplates controls are converted and all other content controls are kept.
`convert` exits with code `1` if any control could not be converted (see the conversion report), and with
code `2` for invalid arguments (unknown options, `--output` without a path, ...).

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
  # Skip outputs of previous conversions
  [[ "$template" == *-templify.docx ]] && continue
  echo "Analyzing: $template"
  ./scripts/analyze.sh "$template"
done
```

**Windows** (in a `.cmd` file; use `call`, otherwise the batch file stops after the first script call):
```cmd
for %%f in (templates\*.docx) do (
    echo Analyzing: %%f
    call scripts\analyze.cmd "%%f"
)
```

### Full Conversion Workflow

**macOS / Linux:**
```bash
#!/bin/bash
# Convert all templates in a directory

for template in old-templates/*.docx; do
  # Skip outputs of previous conversions (convert writes <name>-templify.docx by default)
  [[ "$template" == *-templify.docx ]] && continue

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
REM Note: "call" is required - without it the batch file stops after the first script.

for %%f in (old-templates\*.docx) do (
    echo Processing: %%~nf

    REM Analyze
    call scripts\analyze.cmd "%%f" -o "reports\%%~nf-analysis.md"

    REM Convert
    call scripts\convert.cmd "%%f" -o "new-templates\%%~nf.docx"

    REM Validate
    call scripts\validate.cmd "new-templates\%%~nf.docx"

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
alias tanalyze='/path/to/templify/scripts/analyze.sh'
alias tconvert='/path/to/templify/scripts/convert.sh'
alias tvalidate='/path/to/templify/scripts/validate.sh'
alias tclean='/path/to/templify/scripts/clean.sh'
```

Then use from anywhere:

```bash
tconvert ~/Documents/my-template.docx
tvalidate ~/Documents/my-template-templify.docx
```

### Windows (PowerShell)

Add to PowerShell profile (`$PROFILE`):

```powershell
function tanalyze { & C:\path\to\templify\scripts\analyze.cmd @args }
function tconvert { & C:\path\to\templify\scripts\convert.cmd @args }
function tvalidate { & C:\path\to\templify\scripts\validate.cmd @args }
function tclean { & C:\path\to\templify\scripts\clean.cmd @args }
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

**Cause:** The script is not on your PATH, or it was called without a path

**Solution:**
```bash
# Call it with a path (./ prefix from the repository root, or an absolute path)
./scripts/analyze.sh template.docx
/path/to/templify/scripts/analyze.sh template.docx
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

**Cause:** .NET 10 SDK not installed or not in PATH

**Solution:**
1. Install .NET 10 SDK from https://dotnet.microsoft.com/download
2. Verify installation: `dotnet --version`
3. Restart terminal/command prompt

### Issue: "Project not found"

**Cause:** The script was copied out of the repository. The scripts expect to live in `scripts/` next to
`TriasDev.Templify.Converter/`.

**Solution:** Call the scripts in place (or add the repository's `scripts/` directory to your PATH) instead
of copying them elsewhere. A symlink on your PATH that points to a script is not resolved; add the directory
to PATH or use an alias instead.

## Script Contents

Each script is a thin wrapper around the converter CLI:

**Bash scripts (`.sh`):**
```bash
#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$SCRIPT_DIR/../TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj" -- [command] "$@"
```

**Windows scripts (`.cmd`):**
```cmd
@echo off
dotnet run --project "%~dp0..\TriasDev.Templify.Converter\TriasDev.Templify.Converter.csproj" -- [command] %*
exit /b %ERRORLEVEL%
```

All command-line arguments are passed through to the converter unchanged.

## Related Documentation

- 📖 **[Converter Documentation](../TriasDev.Templify.Converter/README.md)** - Full converter command reference
- 📚 **[Templify Library Documentation](../TriasDev.Templify/README.md)** - Templify API and usage
- 📝 **[Root README](../README.md)** - Repository overview

---

**Part of TriasDev.Templify Project**
© TriasDev GmbH & Co. KG
