// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;

namespace TriasDev.Templify.Converter.Models;

/// <summary>
/// Represents the result of analyzing a template document.
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// All content controls found in the template.
    /// </summary>
    public List<ControlInfo> Controls { get; set; } = new();

    /// <summary>
    /// Count of each control type.
    /// </summary>
    public Dictionary<ControlType, int> TypeCounts { get; set; } = new();

    /// <summary>
    /// Unique variable paths found.
    /// </summary>
    public HashSet<string> UniqueVariablePaths { get; set; } = new();

    /// <summary>
    /// Controls that have complex patterns requiring manual review.
    /// </summary>
    public List<ControlInfo> ComplexControls { get; set; } = new();

    /// <summary>
    /// Warnings encountered during analysis.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Total number of controls found.
    /// </summary>
    public int TotalControls => Controls.Count;

    /// <summary>
    /// Number of unique controls (by tag).
    /// </summary>
    public int UniqueControls => Controls.GroupBy(c => c.Tag).Count();

    /// <summary>
    /// Generate summary statistics.
    /// </summary>
    public void GenerateStatistics()
    {
        // Count by type
        TypeCounts = Controls
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        // Extract unique variable paths
        UniqueVariablePaths = Controls
            .Select(c => c.VariablePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet();

        // Identify complex controls
        ComplexControls = Controls
            .Where(c =>
                c.HasNestedControls ||
                c.Operators.Count > 1 ||
                c.Operators.Contains("or") ||
                c.Operators.Contains("and") ||
                (c.Type == ControlType.Conditional && c.InTableRow))
            .ToList();

        // Generate warnings
        if (ComplexControls.Any())
        {
            Warnings.Add($"Found {ComplexControls.Count} complex controls that may require manual review");
        }

        int nestedCount = Controls.Count(c => c.HasNestedControls);
        if (nestedCount > 0)
        {
            Warnings.Add($"Found {nestedCount} controls with nested controls");
        }

        int tableCount = Controls.Count(c => c.InTable);
        if (tableCount > 0)
        {
            Warnings.Add($"Found {tableCount} controls in tables");
        }
    }

    /// <summary>
    /// Generate a markdown report.
    /// </summary>
    public string GenerateMarkdownReport()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("# Template Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Summary Statistics
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Controls**: {TotalControls}");
        sb.AppendLine($"- **Unique Control Tags**: {UniqueControls}");
        sb.AppendLine($"- **Unique Variable Paths**: {UniqueVariablePaths.Count}");
        sb.AppendLine($"- **Controls Requiring Manual Review**: {ComplexControls.Count}");
        sb.AppendLine();

        // Control Type Breakdown
        sb.AppendLine("## Control Type Breakdown");
        sb.AppendLine();
        foreach (ControlType type in TypeCounts.Keys.OrderBy(k => k))
        {
            sb.AppendLine($"- **{type}**: {TypeCounts[type]} controls");
        }
        sb.AppendLine();

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

        // Complex Controls
        if (ComplexControls.Any())
        {
            sb.AppendLine("## Complex Controls (Manual Review Required)");
            sb.AppendLine();
            sb.AppendLine("| Tag | Type | Reason |");
            sb.AppendLine("|-----|------|--------|");
            foreach (ControlInfo control in ComplexControls)
            {
                string reason = string.Join(", ", control.Notes);
                sb.AppendLine($"| `{control.Tag}` | {control.Type} | {reason} |");
            }
            sb.AppendLine();
        }

        // All Controls by Type
        sb.AppendLine("## All Controls");
        sb.AppendLine();

        foreach (ControlType type in TypeCounts.Keys.OrderBy(k => k))
        {
            sb.AppendLine($"### {type} Controls ({TypeCounts[type]})");
            sb.AppendLine();

            List<ControlInfo> controlsOfType = Controls.Where(c => c.Type == type).ToList();

            sb.AppendLine("| Tag | Variable Path | Templify Syntax |");
            sb.AppendLine("|-----|---------------|-----------------|");

            foreach (ControlInfo control in controlsOfType)
            {
                sb.AppendLine($"| `{control.Tag}` | {control.VariablePath} | `{control.TemplifySyntax}` |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
