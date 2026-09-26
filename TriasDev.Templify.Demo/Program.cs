// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Demo;

/// <summary>
/// Entry point of the Templify demo. The individual demos live in the other partial files:
/// <list type="bullet">
/// <item><c>ComprehensiveDemo.cs</c> - one template covering all features</item>
/// <item><c>JsonDemo.cs</c> - JSON string input</item>
/// <item><c>ValidationDemo.cs</c> - template validation</item>
/// <item><c>CustomTemplateDemo.cs</c> - process your own template and JSON file</item>
/// </list>
/// </summary>
internal partial class Program
{
    private static int Main(string[] args)
    {
        // Optional: process your own template instead of the built-in demos
        //   dotnet run --project TriasDev.Templify.Demo -- --template my.docx|my.odt --data my.json [--output out.docx]
        if (args.Contains("--template") || args.Contains("--data"))
        {
            return RunCustomTemplate(args);
        }

        Console.WriteLine("=== Templify Comprehensive Demo ===");
        Console.WriteLine();

        // Write all demo files to ./output (relative to the current directory) or to the directory
        // passed via --output-dir.
        string outputDir = Path.GetFullPath(GetArgumentValue(args, "--output-dir") ?? "output");
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"📂 Output directory: {outputDir}");
        Console.WriteLine();

        string templatePath = Path.Combine(outputDir, "Templify-Template.docx");
        string outputPath = Path.Combine(outputDir, "Templify-Output.docx");

        Console.WriteLine("🔨 Creating comprehensive template with all use cases...");
        CreateComprehensiveTemplate(templatePath);
        Console.WriteLine($"✅ Template created: {templatePath}");
        Console.WriteLine();

        Console.WriteLine("📊 Creating test data...");
        Dictionary<string, object> data = CreateComprehensiveTestData();
        Console.WriteLine("✅ Test data created");
        Console.WriteLine();

        Console.WriteLine("⚙️  Processing template...");
        ProcessingResult result = ProcessTemplate(templatePath, outputPath, data);

        Console.WriteLine();
        if (result.IsSuccess)
        {
            Console.WriteLine("✅ Template processed successfully!");
            Console.WriteLine($"   Replacements made: {result.ReplacementCount}");

            if (result.MissingVariables.Any())
            {
                Console.WriteLine($"   ⚠️  Missing variables: {string.Join(", ", result.MissingVariables)}");
            }

            if (result.HasWarnings)
            {
                Console.WriteLine($"   ⚠️  {result.Warnings.Count} processing warning(s):");
                foreach (ProcessingWarning warning in result.Warnings)
                {
                    Console.WriteLine($"      - {warning.Type}: {warning.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"📁 Template: {templatePath}");
            Console.WriteLine($"📁 Output:   {outputPath}");
            Console.WriteLine();
            Console.WriteLine("💡 Open both files in Word to compare template vs output!");
        }
        else
        {
            Console.WriteLine($"❌ Processing failed: {result.ErrorMessage}");
        }

        // JSON Demo
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        DemonstrateJsonInput(outputDir);

        // Validation Demo
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        DemonstrateValidation(outputDir);

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("💡 To process your own template, run:");
        Console.WriteLine("   dotnet run --project TriasDev.Templify.Demo -- --template my.docx --data my.json   (or my.odt / my.ott)");

        return result.IsSuccess ? 0 : 1;
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
