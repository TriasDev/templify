// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Demo;

/// <summary>
/// Processes a user-supplied template with a JSON data file:
/// <c>--template &lt;file.docx&gt; --data &lt;file.json&gt; [--output &lt;file.docx&gt;]</c>.
/// </summary>
internal partial class Program
{
    private static int RunCustomTemplate(string[] args)
    {
        Console.WriteLine("=== Custom Template Demo ===");
        Console.WriteLine();

        string? templatePath = GetArgumentValue(args, "--template");
        string? jsonPath = GetArgumentValue(args, "--data");

        if (string.IsNullOrEmpty(templatePath) || string.IsNullOrEmpty(jsonPath))
        {
            Console.WriteLine("Usage: dotnet run --project TriasDev.Templify.Demo -- --template <file.docx> --data <file.json> [--output <file.docx>]");
            return 1;
        }

        templatePath = Path.GetFullPath(templatePath);
        jsonPath = Path.GetFullPath(jsonPath);
        string outputPath = Path.GetFullPath(
            GetArgumentValue(args, "--output")
            ?? Path.Combine(
                Path.GetDirectoryName(templatePath) ?? ".",
                Path.GetFileNameWithoutExtension(templatePath) + "-output.docx"));

        if (!File.Exists(templatePath))
        {
            WriteColored(ConsoleColor.Red, $"❌ Template not found: {templatePath}");
            return 1;
        }

        if (!File.Exists(jsonPath))
        {
            WriteColored(ConsoleColor.Red, $"❌ JSON data not found: {jsonPath}");
            return 1;
        }

        if (string.Equals(templatePath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            WriteColored(ConsoleColor.Red, "❌ The output file must be different from the template file.");
            return 1;
        }

        Console.WriteLine($"📄 Template: {templatePath}");
        Console.WriteLine($"📊 Data:     {jsonPath}");
        Console.WriteLine();

        try
        {
            string jsonData = File.ReadAllText(jsonPath);

            PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
            {
                MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                Culture = CultureInfo.InvariantCulture
            };

            DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

            Console.WriteLine("🔍 Validating template...");
            using (FileStream validateStream = File.OpenRead(templatePath))
            {
                ValidationResult validation = processor.ValidateTemplate(validateStream);

                if (validation.IsValid)
                {
                    WriteColored(ConsoleColor.Green, "   ✅ Template is valid!");
                    Console.WriteLine($"   📝 Found {validation.AllPlaceholders.Count} unique placeholders");
                }
                else
                {
                    WriteColored(ConsoleColor.Yellow, $"   ⚠️  Template has {validation.Errors.Count} validation errors:");
                    foreach (ValidationError error in validation.Errors)
                    {
                        Console.WriteLine($"      - {error.Type}: {error.Message}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("⚙️  Processing template with JSON data...");

            ProcessingResult result;
            using (FileStream templateStream = File.OpenRead(templatePath))
            using (MemoryStream outputStream = new MemoryStream())
            {
                result = processor.ProcessTemplate(templateStream, outputStream, jsonData);
                if (result.IsSuccess)
                {
                    File.WriteAllBytes(outputPath, outputStream.ToArray());
                }
            }

            Console.WriteLine();
            if (!result.IsSuccess)
            {
                WriteColored(ConsoleColor.Red, $"❌ Processing failed: {result.ErrorMessage}");
                return 1;
            }

            WriteColored(ConsoleColor.Green, "✅ Template processed successfully!");
            Console.WriteLine($"   • Replacements made: {result.ReplacementCount}");
            Console.WriteLine($"   • Missing variables: {result.MissingVariables.Count}");
            foreach (string missing in result.MissingVariables.Take(10))
            {
                Console.WriteLine($"      - {missing}");
            }
            if (result.MissingVariables.Count > 10)
            {
                Console.WriteLine($"      ... and {result.MissingVariables.Count - 10} more");
            }

            Console.WriteLine();
            Console.WriteLine($"📁 Output: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            WriteColored(ConsoleColor.Red, $"❌ Exception occurred: {ex.Message}");
            return 1;
        }
    }

    private static void WriteColored(ConsoleColor color, string message)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
