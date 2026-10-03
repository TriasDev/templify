# Templify GUI Test Application

A cross-platform desktop application for testing Templify template processing capabilities. Built with [Avalonia UI](https://avaloniaui.net/) and .NET 10.0.

## Overview

This GUI application provides an interactive interface to:
- Load Word templates (.docx) and LibreOffice / OpenDocument Text templates (.odt, .ott)
- Provide test data in JSON format
- Process templates with Templify
- Preview and save generated documents

## Prerequisites

- .NET 10.0 SDK or later
- Works on Windows, macOS, and Linux

## Running the Application

### From Command Line

Navigate to the project directory and run:

```bash
dotnet run --project TriasDev.Templify.Gui
```

Or from the GUI project directory:

```bash
cd TriasDev.Templify.Gui
dotnet run
```

### From Visual Studio / Rider

1. Open `templify.sln` in Visual Studio or Rider
2. Set `TriasDev.Templify.Gui` as the startup project
3. Press F5 or click Run

## Building for Distribution

### Windows

Build a self-contained executable:

```bash
dotnet publish TriasDev.Templify.Gui -c Release -r win-x64 --self-contained
```

Output: `TriasDev.Templify.Gui/bin/Release/net10.0/win-x64/publish/TriasDev.Templify.Gui.exe`

### macOS

Build a self-contained application:

```bash
dotnet publish TriasDev.Templify.Gui -c Release -r osx-x64 --self-contained
```

Output: `TriasDev.Templify.Gui/bin/Release/net10.0/osx-x64/publish/TriasDev.Templify.Gui`

### Linux

Build a self-contained application:

```bash
dotnet publish TriasDev.Templify.Gui -c Release -r linux-x64 --self-contained
```

Output: `TriasDev.Templify.Gui/bin/Release/net10.0/linux-x64/publish/TriasDev.Templify.Gui`

## Using the Application

1. **Select Template** - Click "Browse..." next to *Template File* and pick a `.docx`, `.odt` or `.ott` file
   containing Templify placeholders (e.g. `{{Name}}`, `{{IsActive:checkbox}}`). The format is detected from the
   file content.
2. **Select JSON Data** - Click "Browse..." next to *JSON Data* and pick a `.json` file, e.g.:
   ```json
   {
     "Name": "John Doe",
     "IsActive": true,
     "Items": [
       { "Name": "Item 1", "Price": 10.00 }
     ]
   }
   ```
3. **Output File** - Defaults to `<template>-output.docx` next to the template, or `<template>-output.odt` for an
   OpenDocument template (an `.ott` template produces an `.odt` document). Click "Browse..." to choose another
   location; an explicitly chosen output file is kept when you change the template or JSON file (only its
   extension switches between `.docx` and `.odt` when the new template has the other format).
   If the output file already exists, a notice tells you that it will be overwritten. The output must
   differ from the template and JSON files.
4. **Options** - Optionally enable HTML entity replacement and choose the formatting culture.
5. **Validate / Process** - "Validate Template" checks the syntax (and missing variables if JSON is selected).
   "Process Template" generates the document. The output is written to a temporary file first and only
   moved into place on success, so a failed run never leaves a broken document behind.
6. **Results** - "Open Output File" opens the generated document. If processing produced warnings,
   "Generate Warning Report" saves a Word report (also for OpenDocument templates) and "Open Warning Report"
   opens it.

## Features

- Full support for all Templify features: placeholders, nested properties, array indexing,
  format specifiers, boolean expressions, conditionals (`if`/`elseif`/`else`) and loops
- Template validation with missing variable detection
- Processing statistics and warnings, with an optional Word warning report
- Culture selection for number, currency and date formatting

## Architecture

The application follows MVVM architecture:
- **Models**: Data structures for templates and processing results
- **ViewModels**: Application logic and state management (using CommunityToolkit.Mvvm)
- **Views**: Avalonia XAML UI definitions
- **Services**: Template processing and file operations

## Development

### Project Structure

```
TriasDev.Templify.Gui/
├── App.axaml            # Application definition
├── App.axaml.cs         # Application startup
├── Program.cs           # Entry point
├── Assets/              # Icons, images
├── Models/              # Data models
├── ViewModels/          # View models
├── Views/               # UI views
└── Services/            # Business logic
```

### Key Dependencies

- **Avalonia UI** - Cross-platform UI framework
- **CommunityToolkit.Mvvm** - MVVM helpers and source generators
- **Microsoft.Extensions.DependencyInjection** - Dependency injection
- **TriasDev.Templify** - Template processing engine

## Debugging

Debug builds attach the [AvaloniaUI Developer Tools](https://docs.avaloniaui.net/tools/developer-tools/installation)
bridge (`AvaloniaUI.DiagnosticsSupport`):
- Install the AvaloniaUI Developer Tools, then press F12 while running in Debug mode
- Inspect visual tree, styles, and data bindings
- Available only in Debug configuration

### Tests

ViewModel and service tests live in `TriasDev.Templify.Tools.Tests` and run headless (no display required):

```bash
dotnet test TriasDev.Templify.Tools.Tests/TriasDev.Templify.Tools.Tests.csproj
```

## Troubleshooting

### Application won't start

1. Ensure .NET 10.0 SDK is installed:
   ```bash
   dotnet --version
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```

### Template processing fails

- Check that the template file is a valid Word document (.docx) or OpenDocument Text document (.odt/.ott);
  legacy `.doc` and flat `.fodt` files are not supported
- Verify JSON data is valid JSON format
- Check the results panel for specific error messages

### JSON errors

- Ensure proper JSON syntax (use quotes for strings, commas between items)
- Use a JSON validator like [jsonlint.com](https://jsonlint.com/)
- Check for trailing commas (not allowed in JSON)

## Related Documentation

- [Main Documentation](../TriasDev.Templify/README.md) - Complete Templify API reference
- [Quick Start Guide](../docs/quick-start.md) - Get started with Templify
- [Format Specifiers Guide](../docs/for-template-authors/format-specifiers.md) - Boolean formatting
- [Boolean Expressions Guide](../docs/for-template-authors/boolean-expressions.md) - Logic evaluation
- [FAQ](../docs/FAQ.md) - Common questions and answers

## Contributing

This is a test application for Templify development. When adding features:
1. Keep the UI simple and focused on testing
2. Add examples for new Templify features
3. Include error handling and validation
4. Update this README if adding major functionality

## License

This test application is part of the Templify project and shares the same license.
