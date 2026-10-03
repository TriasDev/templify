// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Converter.Analyzers;
using TriasDev.Templify.Converter.Cli;
using TriasDev.Templify.Converter.Converters;
using TriasDev.Templify.Converter.Models;
using TriasDev.Templify.Converter.Validators;

namespace TriasDev.Templify.Converter;

/// <summary>
/// CLI entry point.
/// </summary>
/// <remarks>
/// Exit codes: 0 = success, 1 = the command failed (conversion errors, invalid document, I/O error),
/// 2 = invalid command line.
/// </remarks>
public class Program
{
    /// <summary>Exit code for success.</summary>
    public const int ExitSuccess = 0;

    /// <summary>Exit code when the command ran but failed.</summary>
    public const int ExitFailure = 1;

    /// <summary>Exit code for an invalid command line.</summary>
    public const int ExitUsage = 2;

    /// <summary>
    /// Process entry point.
    /// </summary>
    public static int Main(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Run the CLI with the given arguments, writing normal output to <paramref name="stdout"/> and
    /// errors to <paramref name="stderr"/>.
    /// </summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        CommandLineParseResult parse = CommandLineParser.Parse(args);
        if (!parse.IsSuccess)
        {
            stderr.WriteLine($"ERROR: {parse.Error}");
            stderr.WriteLine("Run 'help' for usage information.");
            return ExitUsage;
        }

        ParsedCommandLine commandLine = parse.CommandLine!;

        stdout.WriteLine("TriasDev.Templify.Converter - OpenXMLTemplates to Templify Converter");
        stdout.WriteLine("======================================================================");
        stdout.WriteLine();

        try
        {
            return commandLine.Command switch
            {
                "analyze" => AnalyzeCommand(commandLine, stdout),
                "convert" => ConvertCommand(commandLine, stdout, stderr),
                "validate" => new DocumentValidator(stdout, stderr).ValidateDocument(commandLine.InputPath!) ? ExitSuccess : ExitFailure,
                "clean" => CleanCommand(commandLine, stdout),
                _ => ShowHelp(stdout),
            };
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"ERROR: {ex.Message}");
            if (commandLine.Verbose)
            {
                stderr.WriteLine(ex.ToString());
            }

            return ExitFailure;
        }
    }

    private static int AnalyzeCommand(ParsedCommandLine commandLine, TextWriter stdout)
    {
        string templatePath = commandLine.InputPath!;
        string outputPath = commandLine.OutputPath ?? Path.Combine(
            Path.GetDirectoryName(templatePath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(templatePath)}-analysis-report.md");

        stdout.WriteLine($"Analyzing template: {templatePath}");
        stdout.WriteLine();

        AnalysisResult result = new TemplateAnalyzer().AnalyzeTemplate(templatePath);

        stdout.WriteLine("✓ Analysis complete!");
        stdout.WriteLine();
        stdout.WriteLine("SUMMARY:");
        stdout.WriteLine($"  Total controls: {result.TotalControls}");
        stdout.WriteLine($"  Unique control tags: {result.UniqueControls}");
        stdout.WriteLine($"  Unique variable paths: {result.UniqueVariablePaths.Count}");
        stdout.WriteLine();

        stdout.WriteLine("CONTROL TYPES:");
        foreach (KeyValuePair<ControlType, int> kvp in result.TypeCounts.OrderBy(k => k.Key))
        {
            stdout.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        stdout.WriteLine();

        if (result.ComplexControls.Count > 0)
        {
            stdout.WriteLine($"⚠️  {result.ComplexControls.Count} controls require manual review");
            stdout.WriteLine();
        }

        if (result.Warnings.Count > 0)
        {
            stdout.WriteLine("WARNINGS:");
            foreach (string warning in result.Warnings)
            {
                stdout.WriteLine($"  - {warning}");
            }

            stdout.WriteLine();
        }

        File.WriteAllText(outputPath, result.GenerateMarkdownReport());
        stdout.WriteLine($"✓ Report saved to: {outputPath}");
        stdout.WriteLine();

        return ExitSuccess;
    }

    private static int ConvertCommand(ParsedCommandLine commandLine, TextWriter stdout, TextWriter stderr)
    {
        string templatePath = commandLine.InputPath!;
        string outputPath = commandLine.OutputPath ?? Path.Combine(
            Path.GetDirectoryName(templatePath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(templatePath)}-templify.docx");

        stdout.WriteLine($"Converting template: {templatePath}");
        stdout.WriteLine(SafeFileWriter.IsSamePath(templatePath, outputPath)
            ? "Note: Template will be converted in-place"
            : $"Output will be saved to: {outputPath}");
        stdout.WriteLine();

        TemplateConverter converter = new TemplateConverter(new ConversionOptions
        {
            UnwrapAllControls = commandLine.UnwrapAllControls,
        });
        ConversionResult result = converter.ConvertTemplate(templatePath, outputPath);

        stdout.WriteLine(result.Success ? "✓ Conversion complete!" : "✗ Conversion finished with errors");
        stdout.WriteLine();
        stdout.WriteLine("SUMMARY:");
        stdout.WriteLine($"  Total controls: {result.TotalControls}");
        stdout.WriteLine($"  Successfully converted: {result.ConvertedControls}");
        stdout.WriteLine($"  Failed (kept for manual conversion): {result.SkippedControls}");
        if (commandLine.UnwrapAllControls)
        {
            stdout.WriteLine($"  Other content controls unwrapped: {result.CleanedSdtElements}");
        }

        stdout.WriteLine();

        if (result.ConversionsByType.Count > 0)
        {
            stdout.WriteLine("CONVERSIONS BY TYPE:");
            foreach (KeyValuePair<ControlType, int> kvp in result.ConversionsByType.OrderBy(k => k.Key))
            {
                stdout.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            stdout.WriteLine();
        }

        if (result.Warnings.Count > 0)
        {
            stdout.WriteLine($"⚠️  {result.Warnings.Count} warnings (review recommended):");
            WriteList(stdout, result.Warnings);
        }

        if (result.Errors.Count > 0)
        {
            stderr.WriteLine($"❌ {result.Errors.Count} errors:");
            WriteList(stderr, result.Errors);
        }

        string reportPath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(outputPath)}-conversion-report.md");
        File.WriteAllText(reportPath, result.GenerateMarkdownReport());

        stdout.WriteLine($"✓ Converted template saved to: {outputPath}");
        stdout.WriteLine($"✓ Conversion report saved to: {reportPath}");
        stdout.WriteLine();

        return result.Success ? ExitSuccess : ExitFailure;
    }

    private static void WriteList(TextWriter writer, List<string> items)
    {
        foreach (string item in items.Take(10))
        {
            writer.WriteLine($"  - {item}");
        }

        if (items.Count > 10)
        {
            writer.WriteLine($"  ... and {items.Count - 10} more (see report)");
        }

        writer.WriteLine();
    }

    private static int CleanCommand(ParsedCommandLine commandLine, TextWriter stdout)
    {
        string documentPath = commandLine.InputPath!;
        string outputPath = commandLine.OutputPath ?? documentPath;
        bool inPlace = SafeFileWriter.IsSamePath(documentPath, outputPath);

        stdout.WriteLine($"Cleaning document: {documentPath}");
        stdout.WriteLine(inPlace
            ? "Note: Document will be cleaned in-place"
            : $"Output will be saved to: {outputPath}");
        stdout.WriteLine();

        int removedCount = DocumentCleaner.CleanDocument(documentPath, outputPath);

        stdout.WriteLine("✓ Document cleaned successfully!");
        stdout.WriteLine($"Removed {removedCount} SDT element(s)");
        stdout.WriteLine(removedCount > 0
            ? $"✓ Cleaned document saved to: {outputPath}"
            : "✓ No SDT elements found - document was already clean");

        return ExitSuccess;
    }

    private static int ShowHelp(TextWriter stdout)
    {
        stdout.WriteLine("USAGE:");
        stdout.WriteLine("  dotnet run -- <command> <document> [options]");
        stdout.WriteLine();
        stdout.WriteLine("COMMANDS:");
        stdout.WriteLine("  analyze <template-path> [--output <report-path>]");
        stdout.WriteLine("      Analyze a template and generate a report");
        stdout.WriteLine();
        stdout.WriteLine("  convert <template-path> [--output <output-path>] [--unwrap-all-controls]");
        stdout.WriteLine("      Convert a template from OpenXMLTemplates to Templify.");
        stdout.WriteLine("      Only OpenXMLTemplates controls are converted; other content controls are kept");
        stdout.WriteLine("      unless --unwrap-all-controls is given.");
        stdout.WriteLine();
        stdout.WriteLine("  validate <document-path>");
        stdout.WriteLine("      Validate that a document is well-formed and can be opened");
        stdout.WriteLine();
        stdout.WriteLine("  clean <document-path> [--output <output-path>]");
        stdout.WriteLine("      Remove all SDT elements from a document (in-place unless --output is given)");
        stdout.WriteLine();
        stdout.WriteLine("  help");
        stdout.WriteLine("      Show this help message");
        stdout.WriteLine();
        stdout.WriteLine("OPTIONS:");
        stdout.WriteLine("  -o, --output <path>   Output path (also accepted as --output=<path>, before or after the input)");
        stdout.WriteLine("  -v, --verbose         Print stack traces for unexpected errors");
        stdout.WriteLine();
        stdout.WriteLine("EXIT CODES:");
        stdout.WriteLine("  0 success, 1 command failed (e.g. a control could not be converted), 2 invalid arguments");
        stdout.WriteLine();
        stdout.WriteLine("EXAMPLES:");
        stdout.WriteLine("  dotnet run -- analyze template.docx");
        stdout.WriteLine("  dotnet run -- convert template.docx --output converted.docx");
        stdout.WriteLine();
        return ExitSuccess;
    }
}
