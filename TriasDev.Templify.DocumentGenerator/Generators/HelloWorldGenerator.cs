using DocumentFormat.OpenXml.Packaging;

namespace TriasDev.Templify.DocumentGenerator.Generators;

/// <summary>
/// Generates a simple "Hello World" example demonstrating basic placeholder replacement
/// </summary>
public class HelloWorldGenerator : BaseExampleGenerator
{
    public override string Name => "hello-world";

    public override string Description => "Simple placeholder replacement with text and numbers";

    public override string GenerateTemplate(string outputDirectory)
    {
        var templatePath = Path.Combine(outputDirectory, $"{Name}-template.docx");

        using (var doc = CreateDocument(templatePath))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            // Title
            AddParagraph(body, "Hello World Template", isBold: true);
            AddEmptyParagraph(body);

            // Simple placeholders
            AddParagraph(body, "Hello {{FirstName}} {{LastName}}!");
            AddEmptyParagraph(body);

            AddParagraph(body, "Welcome to Templify. Today is {{Date}} and you are customer #{{CustomerNumber}}.");
            AddEmptyParagraph(body);

            // Boolean and numeric placeholders
            AddParagraph(body, "Account Status: {{IsActive}}");
            AddParagraph(body, "Balance: {{Balance}} EUR");
            AddEmptyParagraph(body);

            // Nested data example
            AddParagraph(body, "Company Information:", isBold: true);
            AddParagraph(body, "Company: {{Company.Name}}");
            AddParagraph(body, "Location: {{Company.City}}, {{Company.Country}}");
            AddParagraph(body, "Email: {{Company.Email}}");

            doc.Save();
        }

        return templatePath;
    }

    public override Dictionary<string, object> GetSampleData()
    {
        return new Dictionary<string, object>
        {
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["CustomerNumber"] = 12345,
            ["IsActive"] = true,
            ["Balance"] = 1250.50m,
            ["Company"] = new Dictionary<string, object>
            {
                ["Name"] = "Acme Corporation",
                ["City"] = "Springfield",
                ["Country"] = "USA",
                ["Email"] = "contact@acme.com"
            }
        };
    }
}
