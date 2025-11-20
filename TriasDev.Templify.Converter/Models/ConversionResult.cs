// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;

namespace TriasDev.Templify.Converter.Models;

/// <summary>
/// Represents the result of converting a template.
/// </summary>
public class ConversionResult
{
    /// <summary>
    /// Total number of controls processed.
    /// </summary>
    public int TotalControls { get; set; }

    /// <summary>
    /// Number of controls successfully converted.
    /// </summary>
    public int ConvertedControls { get; set; }

    /// <summary>
    /// Number of controls skipped (not converted).
    /// </summary>
    public int SkippedControls { get; set; }

    /// <summary>
    /// Controls converted by type.
    /// </summary>
    public Dictionary<ControlType, int> ConversionsByType { get; set; } = new();

    /// <summary>
    /// List of warnings encountered during conversion.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// List of errors encountered during conversion.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Controls that could not be converted automatically.
    /// </summary>
    public List<ControlInfo> FailedConversions { get; set; } = new();

    /// <summary>
    /// Number of SDT elements cleaned up after conversion.
    /// </summary>
    public int CleanedSdtElements { get; set; }

    /// <summary>
    /// Was the conversion successful overall?
    /// </summary>
    public bool Success => Errors.Count == 0;

    /// <summary>
    /// Generate a markdown report of the conversion.
    /// </summary>
    public string GenerateMarkdownReport()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("# Template Conversion Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Controls**: {TotalControls}");
        sb.AppendLine($"- **Successfully Converted**: {ConvertedControls}");
        sb.AppendLine($"- **Skipped**: {SkippedControls}");
        sb.AppendLine($"- **Cleaned SDT Elements**: {CleanedSdtElements}");
        sb.AppendLine($"- **Status**: {(Success ? "✓ Success" : "✗ Failed")}");
        sb.AppendLine();

        // Conversions by Type
        if (ConversionsByType.Any())
        {
            sb.AppendLine("## Conversions by Type");
            sb.AppendLine();
            foreach (KeyValuePair<ControlType, int> kvp in ConversionsByType.OrderBy(k => k.Key))
            {
                sb.AppendLine($"- **{kvp.Key}**: {kvp.Value} converted");
            }
            sb.AppendLine();
        }

        // Warnings
        if (Warnings.Any())
        {
            sb.AppendLine("## ⚠️ Warnings");
            sb.AppendLine();
            foreach (string warning in Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
            sb.AppendLine();
        }

        // Errors
        if (Errors.Any())
        {
            sb.AppendLine("## ❌ Errors");
            sb.AppendLine();
            foreach (string error in Errors)
            {
                sb.AppendLine($"- {error}");
            }
            sb.AppendLine();
        }

        // Failed Conversions
        if (FailedConversions.Any())
        {
            sb.AppendLine("## Failed Conversions (Require Manual Review)");
            sb.AppendLine();
            sb.AppendLine("| Tag | Type | Reason |");
            sb.AppendLine("|-----|------|--------|");
            foreach (ControlInfo control in FailedConversions)
            {
                string reason = string.Join(", ", control.Notes);
                sb.AppendLine($"| `{control.Tag}` | {control.Type} | {reason} |");
            }
            sb.AppendLine();
        }

        // Next Steps
        sb.AppendLine("## Next Steps");
        sb.AppendLine();
        if (Success)
        {
            sb.AppendLine("1. Review the converted template in Word");
            sb.AppendLine("2. Check that placeholders are correctly formatted");
            sb.AppendLine("3. Test with sample data using Templify");
            sb.AppendLine("4. Compare output with original template");
        }
        else
        {
            sb.AppendLine("1. Review the errors listed above");
            sb.AppendLine("2. Fix any issues in the template");
            sb.AppendLine("3. Re-run the conversion");
        }
        sb.AppendLine();

        return sb.ToString();
    }
}
