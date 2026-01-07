using DocumentFormat.OpenXml.Packaging;

namespace TriasDev.Templify.DocumentGenerator.Generators;

/// <summary>
/// Generates the warning report template used by Templify to render processing warnings.
/// This template is embedded in the main library as a resource.
/// </summary>
public class WarningReportTemplateGenerator : BaseExampleGenerator
{
    public override string Name => "warning-report";

    public override string Description => "Warning report template for Templify processing warnings";

    public override string GenerateTemplate(string outputDirectory)
    {
        var templatePath = Path.Combine(outputDirectory, $"{Name}-template.docx");

        using (var doc = CreateDocument(templatePath))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            // Title
            AddParagraph(body, "Template Processing Warning Report", isBold: true);
            AddEmptyParagraph(body);

            // Metadata
            AddParagraph(body, "Generated: {{GeneratedAt}}");
            AddEmptyParagraph(body);

            // Summary section
            AddParagraph(body, "Summary", isBold: true);
            AddParagraph(body, "Total Warnings: {{TotalWarnings}}");
            AddEmptyParagraph(body);

            // Summary table
            var summaryTable = CreateTable(2);
            AddTableHeaderRow(summaryTable, "Warning Type", "Count");
            AddTableRow(summaryTable, "Missing Variables", "{{MissingVariableCount}}");
            AddTableRow(summaryTable, "Missing Collections", "{{MissingCollectionCount}}");
            AddTableRow(summaryTable, "Null Collections", "{{NullCollectionCount}}");
            body.AppendChild(summaryTable);
            AddEmptyParagraph(body);

            // Missing Variables section (conditional)
            AddParagraph(body, "{{#if HasMissingVariables}}");
            AddParagraph(body, "Missing Variables", isBold: true);
            AddParagraph(body, "The following variables were referenced in the template but not found in the data:");
            AddEmptyParagraph(body);

            var missingVarsTable = CreateTable(2);
            AddTableHeaderRow(missingVarsTable, "Variable Name", "Context");
            AddTableRow(missingVarsTable, "{{#foreach MissingVariables}}", "");
            AddTableRow(missingVarsTable, "{{VariableName}}", "{{Context}}");
            AddTableRow(missingVarsTable, "{{/foreach}}", "");
            body.AppendChild(missingVarsTable);
            AddEmptyParagraph(body);
            AddParagraph(body, "{{/if}}");

            // Missing Collections section (conditional)
            AddParagraph(body, "{{#if HasMissingCollections}}");
            AddParagraph(body, "Missing Loop Collections", isBold: true);
            AddParagraph(body, "The following collections were referenced in loops but not found in the data:");
            AddEmptyParagraph(body);

            var missingCollTable = CreateTable(2);
            AddTableHeaderRow(missingCollTable, "Collection Name", "Context");
            AddTableRow(missingCollTable, "{{#foreach MissingCollections}}", "");
            AddTableRow(missingCollTable, "{{VariableName}}", "{{Context}}");
            AddTableRow(missingCollTable, "{{/foreach}}", "");
            body.AppendChild(missingCollTable);
            AddEmptyParagraph(body);
            AddParagraph(body, "{{/if}}");

            // Null Collections section (conditional)
            AddParagraph(body, "{{#if HasNullCollections}}");
            AddParagraph(body, "Null Loop Collections", isBold: true);
            AddParagraph(body, "The following collections were found but had null values:");
            AddEmptyParagraph(body);

            var nullCollTable = CreateTable(2);
            AddTableHeaderRow(nullCollTable, "Collection Name", "Context");
            AddTableRow(nullCollTable, "{{#foreach NullCollections}}", "");
            AddTableRow(nullCollTable, "{{VariableName}}", "{{Context}}");
            AddTableRow(nullCollTable, "{{/foreach}}", "");
            body.AppendChild(nullCollTable);
            AddEmptyParagraph(body);
            AddParagraph(body, "{{/if}}");

            // Footer
            AddEmptyParagraph(body);
            AddParagraph(body, "End of Warning Report");

            doc.Save();
        }

        return templatePath;
    }

    public override Dictionary<string, object> GetSampleData()
    {
        // Sample data for testing the template
        return new Dictionary<string, object>
        {
            ["GeneratedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["TotalWarnings"] = 5,
            ["MissingVariableCount"] = 2,
            ["MissingCollectionCount"] = 2,
            ["NullCollectionCount"] = 1,
            ["HasMissingVariables"] = true,
            ["HasMissingCollections"] = true,
            ["HasNullCollections"] = true,
            ["MissingVariables"] = new List<Dictionary<string, object>>
            {
                new() { ["VariableName"] = "CustomerName", ["Context"] = "placeholder" },
                new() { ["VariableName"] = "Customer.Email", ["Context"] = "placeholder" }
            },
            ["MissingCollections"] = new List<Dictionary<string, object>>
            {
                new() { ["VariableName"] = "OrderItems", ["Context"] = "loop: OrderItems" },
                new() { ["VariableName"] = "Categories", ["Context"] = "loop: Categories" }
            },
            ["NullCollections"] = new List<Dictionary<string, object>>
            {
                new() { ["VariableName"] = "Products", ["Context"] = "loop: Products" }
            }
        };
    }
}
