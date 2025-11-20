// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Converter.Analyzers;
using TriasDev.Templify.Converter.Converters;
using TriasDev.Templify.Converter.Models;
using TriasDev.Templify.Converter.Validators;

namespace TriasDev.Templify.Converter;

public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("TriasDev.Templify.Converter - OpenXMLTemplates to Templify Converter");
        Console.WriteLine("======================================================================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "analyze" => AnalyzeCommand(args),
                "convert" => ConvertCommand(args),
                "validate" => ValidateCommand(args),
                "clean" => CleanCommand(args),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => ShowInvalidCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private static int AnalyzeCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("ERROR: analyze command requires a template file path");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run -- analyze <template-path> [--output <report-path>]");
            return 1;
        }

        string templatePath = args[1];
        string? outputPath = null;

        // Parse optional output path
        for (int i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "--output" || args[i] == "-o")
            {
                outputPath = args[i + 1];
                break;
            }
        }

        Console.WriteLine($"Analyzing template: {templatePath}");
        Console.WriteLine();

        TemplateAnalyzer analyzer = new TemplateAnalyzer();
        AnalysisResult result = analyzer.AnalyzeTemplate(templatePath);

        // Print summary to console
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Analysis complete!");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("SUMMARY:");
        Console.WriteLine($"  Total controls: {result.TotalControls}");
        Console.WriteLine($"  Unique control tags: {result.UniqueControls}");
        Console.WriteLine($"  Unique variable paths: {result.UniqueVariablePaths.Count}");
        Console.WriteLine();

        Console.WriteLine("CONTROL TYPES:");
        foreach (KeyValuePair<ControlType, int> kvp in result.TypeCounts.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine();

        if (result.ComplexControls.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  {result.ComplexControls.Count} controls require manual review");
            Console.ResetColor();
            Console.WriteLine();
        }

        if (result.Warnings.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNINGS:");
            foreach (string warning in result.Warnings)
            {
                Console.WriteLine($"  - {warning}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Generate markdown report
        if (outputPath == null)
        {
            // Default output path
            string dir = Path.GetDirectoryName(templatePath) ?? ".";
            string filename = Path.GetFileNameWithoutExtension(templatePath);
            outputPath = Path.Combine(dir, $"{filename}-analysis-report.md");
        }

        string reportContent = result.GenerateMarkdownReport();
        File.WriteAllText(outputPath, reportContent);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Report saved to: {outputPath}");
        Console.ResetColor();
        Console.WriteLine();

        return 0;
    }

    private static int ConvertCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("ERROR: convert command requires a template file path");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run -- convert <template-path> [--output <output-path>]");
            return 1;
        }

        string templatePath = args[1];
        string? outputPath = null;

        // Parse optional output path
        for (int i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "--output" || args[i] == "-o")
            {
                outputPath = args[i + 1];
                break;
            }
        }

        // Default output path
        if (outputPath == null)
        {
            string dir = Path.GetDirectoryName(templatePath) ?? ".";
            string filename = Path.GetFileNameWithoutExtension(templatePath);
            outputPath = Path.Combine(dir, $"{filename}-templify.docx");
        }

        Console.WriteLine($"Converting template: {templatePath}");
        Console.WriteLine($"Output will be saved to: {outputPath}");
        Console.WriteLine();

        TemplateConverter converter = new TemplateConverter();
        ConversionResult result = converter.ConvertTemplate(templatePath, outputPath);

        // Print summary to console
        Console.WriteLine();
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Conversion complete!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ Conversion failed with errors");
            Console.ResetColor();
        }
        Console.WriteLine();

        Console.WriteLine("SUMMARY:");
        Console.WriteLine($"  Total controls: {result.TotalControls}");
        Console.WriteLine($"  Successfully converted: {result.ConvertedControls}");
        Console.WriteLine($"  Skipped: {result.SkippedControls}");
        Console.WriteLine($"  Cleaned SDT elements: {result.CleanedSdtElements}");
        Console.WriteLine();

        if (result.ConversionsByType.Any())
        {
            Console.WriteLine("CONVERSIONS BY TYPE:");
            foreach (KeyValuePair<ControlType, int> kvp in result.ConversionsByType.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            Console.WriteLine();
        }

        if (result.Warnings.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  {result.Warnings.Count} warnings:");
            foreach (string warning in result.Warnings.Take(10))
            {
                Console.WriteLine($"  - {warning}");
            }
            if (result.Warnings.Count > 10)
            {
                Console.WriteLine($"  ... and {result.Warnings.Count - 10} more (see report)");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        if (result.Errors.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ {result.Errors.Count} errors:");
            foreach (string error in result.Errors.Take(10))
            {
                Console.WriteLine($"  - {error}");
            }
            if (result.Errors.Count > 10)
            {
                Console.WriteLine($"  ... and {result.Errors.Count - 10} more (see report)");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Generate conversion report
        string reportPath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(outputPath)}-conversion-report.md"
        );

        string reportContent = result.GenerateMarkdownReport();
        File.WriteAllText(reportPath, reportContent);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Converted template saved to: {outputPath}");
        Console.WriteLine($"✓ Conversion report saved to: {reportPath}");
        Console.ResetColor();
        Console.WriteLine();

        return result.Success ? 0 : 1;
    }

    private static int ValidateCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("ERROR: validate command requires a document path");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run -- validate <document-path>");
            return 1;
        }

        string documentPath = args[1];

        DocumentValidator validator = new DocumentValidator();
        bool isValid = validator.ValidateDocument(documentPath);

        return isValid ? 0 : 1;
    }

    private static int CleanCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("ERROR: clean command requires a document path");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run -- clean <document-path> [--output <output-path>]");
            return 1;
        }

        string documentPath = args[1];
        string? outputPath = null;

        // Parse optional output path
        for (int i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "--output" || args[i] == "-o")
            {
                outputPath = args[i + 1];
                break;
            }
        }

        // Default output path (overwrite input)
        if (outputPath == null)
        {
            outputPath = documentPath;
        }

        Console.WriteLine($"Cleaning document: {documentPath}");
        if (outputPath != documentPath)
        {
            Console.WriteLine($"Output will be saved to: {outputPath}");
        }
        else
        {
            Console.WriteLine("Note: Document will be cleaned in-place");
        }
        Console.WriteLine();

        try
        {
            int removedCount = DocumentCleaner.CleanDocument(documentPath, outputPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Document cleaned successfully!");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Removed {removedCount} SDT element(s)");
            Console.WriteLine();

            if (removedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Cleaned document saved to: {outputPath}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("✓ No SDT elements found - document was already clean");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Cleaning failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int ShowHelp()
    {
        Console.WriteLine("USAGE:");
        Console.WriteLine("  dotnet run -- <command> [options]");
        Console.WriteLine();
        Console.WriteLine("COMMANDS:");
        Console.WriteLine("  analyze <template-path> [--output <report-path>]");
        Console.WriteLine("      Analyze a template and generate a report");
        Console.WriteLine();
        Console.WriteLine("  convert <template-path> [--output <output-path>]");
        Console.WriteLine("      Convert a template from OpenXMLTemplates to Templify");
        Console.WriteLine();
        Console.WriteLine("  validate <document-path>");
        Console.WriteLine("      Validate that a document is well-formed and can be opened");
        Console.WriteLine();
        Console.WriteLine("  clean <document-path> [--output <output-path>]");
        Console.WriteLine("      Remove all SDT elements from a document (fixes corrupted templates)");
        Console.WriteLine();
        Console.WriteLine("  help");
        Console.WriteLine("      Show this help message");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  dotnet run -- analyze template.docx");
        Console.WriteLine("  dotnet run -- analyze template.docx --output report.md");
        Console.WriteLine();
        return 0;
    }

    private static int ShowInvalidCommand(string command)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: Unknown command '{command}'");
        Console.ResetColor();
        Console.WriteLine();
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run -- <command> [options]");
        Console.WriteLine("Run 'dotnet run -- help' for more information");
    }
}
