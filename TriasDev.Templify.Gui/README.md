# Templify GUI Test Application

A cross-platform desktop application for testing Templify template processing capabilities. Built with [Avalonia UI](https://avaloniaui.net/) and .NET 9.0.

## Overview

This GUI application provides an interactive interface to:
- Load Word document templates (.docx)
- Provide test data in JSON format
- Process templates with Templify
- Preview and save generated documents

## Prerequisites

- .NET 9.0 SDK or later
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

### From VS Code

1. Open the workspace in VS Code
2. Select the "Launch Templify GUI" configuration from the Run menu
3. Press F5 to start debugging

## Building for Distribution

### Windows

Build a self-contained executable:

```bash
dotnet publish TriasDev.Templify.Gui -c Release -r win-x64 --self-contained
```

Output: `TriasDev.Templify.Gui/bin/Release/net9.0/win-x64/publish/TriasDev.Templify.Gui.exe`

### macOS

Build a self-contained application:

```bash
dotnet publish TriasDev.Templify.Gui -c Release -r osx-x64 --self-contained
```

Output: `TriasDev.Templify.Gui/bin/Release/net9.0/osx-x64/publish/TriasDev.Templify.Gui`

### Linux

Build a self-contained application:

```bash
dotnet publish TriasDev.Templify.Gui -c Release -r linux-x64 --self-contained
```

Output: `TriasDev.Templify.Gui/bin/Release/net9.0/linux-x64/publish/TriasDev.Templify.Gui`

## Using the Application

1. **Load Template**
   - Click "Load Template" or drag-and-drop a `.docx` file
   - The template should contain Templify placeholders (e.g., `{{Name}}`, `{{IsActive:checkbox}}`)

2. **Provide Test Data**
   - Enter JSON data in the data panel
   - Example:
     ```json
     {
       "Name": "John Doe",
       "IsActive": true,
       "Items": [
         { "Name": "Item 1", "Price": 10.00 }
       ]
     }
     ```

3. **Process Template**
   - Click "Process" to generate the document
   - View processing results and statistics

4. **Save Result**
   - Click "Save" to export the generated document
   - Choose location and filename

## Features

### Template Processing
- Full support for all Templify features:
  - Simple placeholders: `{{Name}}`
  - Nested properties: `{{User.Email}}`
  - Array indexing: `{{Items[0]}}`
  - Format specifiers: `{{IsActive:checkbox}}`
  - Boolean expressions: `{{(Age >= 18):yesno}}`
  - Conditionals: `{{#if Active}}...{{/if}}`
  - Loops: `{{#foreach Items}}...{{/foreach}}`

### JSON Data Input
- Syntax highlighting
- Validation
- Auto-formatting

### Error Reporting
- Clear error messages
- Missing variable detection
- Processing statistics

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
├── ViewLocator.cs       # View resolution
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

The application includes Avalonia DevTools for debugging:
- Press F12 while running in Debug mode
- Inspect visual tree, styles, and data bindings
- Available only in Debug configuration

## Troubleshooting

### Application won't start

1. Ensure .NET 9.0 SDK is installed:
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

- Check that the template file is a valid Word document (.docx)
- Verify JSON data is valid JSON format
- Check the error panel for specific error messages

### JSON validation errors

- Ensure proper JSON syntax (use quotes for strings, commas between items)
- Use a JSON validator like [jsonlint.com](https://jsonlint.com/)
- Check for trailing commas (not allowed in JSON)

## Related Documentation

- [Main Documentation](../TriasDev.Templify/README.md) - Complete Templify API reference
- [Quick Start Guide](../docs/quick-start.md) - Get started with Templify
- [Format Specifiers Guide](../docs/guides/format-specifiers.md) - Boolean formatting
- [Boolean Expressions Guide](../docs/guides/boolean-expressions.md) - Logic evaluation
- [FAQ](../docs/FAQ.md) - Common questions and answers

## Contributing

This is a test application for Templify development. When adding features:
1. Keep the UI simple and focused on testing
2. Add examples for new Templify features
3. Include error handling and validation
4. Update this README if adding major functionality

## License

This test application is part of the Templify project and shares the same license.
