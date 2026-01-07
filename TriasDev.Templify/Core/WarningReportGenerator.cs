// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace TriasDev.Templify.Core;

/// <summary>
/// Internal generator for creating warning report documents from processing warnings.
/// Uses an embedded template and Templify itself to render the report.
/// </summary>
internal static class WarningReportGenerator
{
    private const string TemplateResourceName = "TriasDev.Templify.Resources.WarningReportTemplate.docx";

    /// <summary>
    /// Generates a warning report document from the provided warnings.
    /// </summary>
    /// <param name="warnings">The warnings to include in the report.</param>
    /// <returns>A MemoryStream containing the generated .docx report.</returns>
    public static MemoryStream GenerateReport(IReadOnlyList<ProcessingWarning> warnings)
    {
        // Load template from embedded resources
        using Stream templateStream = LoadTemplate();

        // Build data dictionary from warnings
        Dictionary<string, object> data = BuildReportData(warnings);

        // Process template with Templify
        MemoryStream outputStream = new MemoryStream();
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        processor.ProcessTemplate(templateStream, outputStream, data);

        // Reset position for reading
        outputStream.Position = 0;
        return outputStream;
    }

    /// <summary>
    /// Generates a warning report document and returns it as a byte array.
    /// </summary>
    /// <param name="warnings">The warnings to include in the report.</param>
    /// <returns>A byte array containing the generated .docx report.</returns>
    public static byte[] GenerateReportBytes(IReadOnlyList<ProcessingWarning> warnings)
    {
        using MemoryStream stream = GenerateReport(warnings);
        return stream.ToArray();
    }

    private static Stream LoadTemplate()
    {
        Assembly assembly = typeof(WarningReportGenerator).Assembly;
        Stream? stream = assembly.GetManifestResourceStream(TemplateResourceName);

        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{TemplateResourceName}' not found. " +
                "This is a bug in Templify - please report it.");
        }

        return stream;
    }

    private static Dictionary<string, object> BuildReportData(IReadOnlyList<ProcessingWarning> warnings)
    {
        // Categorize warnings by type
        List<Dictionary<string, object>> missingVariables = new();
        List<Dictionary<string, object>> missingCollections = new();
        List<Dictionary<string, object>> nullCollections = new();
        List<Dictionary<string, object>> failedExpressions = new();

        foreach (ProcessingWarning warning in warnings)
        {
            Dictionary<string, object> warningData = new()
            {
                ["VariableName"] = warning.VariableName ?? "",
                ["Context"] = warning.Context ?? "",
                ["Message"] = warning.Message
            };

            switch (warning.Type)
            {
                case ProcessingWarningType.MissingVariable:
                    missingVariables.Add(warningData);
                    break;
                case ProcessingWarningType.MissingLoopCollection:
                    missingCollections.Add(warningData);
                    break;
                case ProcessingWarningType.NullLoopCollection:
                    nullCollections.Add(warningData);
                    break;
                case ProcessingWarningType.ExpressionFailed:
                    failedExpressions.Add(warningData);
                    break;
            }
        }

        return new Dictionary<string, object>
        {
            ["GeneratedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["TotalWarnings"] = warnings.Count,
            ["MissingVariableCount"] = missingVariables.Count,
            ["MissingCollectionCount"] = missingCollections.Count,
            ["NullCollectionCount"] = nullCollections.Count,
            ["FailedExpressionCount"] = failedExpressions.Count,
            ["HasMissingVariables"] = missingVariables.Count > 0,
            ["HasMissingCollections"] = missingCollections.Count > 0,
            ["HasNullCollections"] = nullCollections.Count > 0,
            ["HasFailedExpressions"] = failedExpressions.Count > 0,
            ["MissingVariables"] = missingVariables,
            ["MissingCollections"] = missingCollections,
            ["NullCollections"] = nullCollections,
            ["FailedExpressions"] = failedExpressions
        };
    }
}
