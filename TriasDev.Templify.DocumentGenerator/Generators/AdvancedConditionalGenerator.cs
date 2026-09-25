using System.Globalization;

namespace TriasDev.Templify.DocumentGenerator.Generators;

/// <summary>
/// Generates an example demonstrating elseif chains and the condition operators
/// <c>in</c>, <c>contains</c>, <c>startswith</c>, <c>exists</c>, <c>is empty</c> / <c>is not empty</c>
/// and grouping with parentheses.
/// </summary>
public class AdvancedConditionalGenerator : BaseExampleGenerator
{
    public override string Name => "advanced-conditionals";

    public override string Description => "elseif chains, in/contains/startswith, exists/is empty and grouping with parentheses";

    public override string GenerateTemplate(string outputDirectory)
    {
        var templatePath = Path.Combine(outputDirectory, $"{Name}-template.docx");

        using (var doc = CreateDocument(templatePath))
        {
            var body = doc.MainDocumentPart!.Document!.Body!;

            // Title
            AddParagraph(body, "Account Review", isBold: true);
            AddEmptyParagraph(body);

            AddParagraph(body, "Member: {{MemberName}} ({{MemberId}})");
            AddParagraph(body, "Review date: {{ReviewDate}}");
            AddEmptyParagraph(body);

            // elseif chain
            AddParagraph(body, "Membership Tier (elseif):", isBold: true);
            AddParagraph(body, "{{#if Points >= 1000}}");
            AddParagraph(body, "🥇 Gold member - {{Points}} points");
            AddParagraph(body, "{{#elseif Points >= 500}}");
            AddParagraph(body, "🥈 Silver member - {{Points}} points");
            AddParagraph(body, "{{#elseif Points > 0}}");
            AddParagraph(body, "🥉 Bronze member - {{Points}} points");
            AddParagraph(body, "{{#else}}");
            AddParagraph(body, "No points collected yet.");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Membership with in
            AddParagraph(body, "Permissions (in):", isBold: true);
            AddParagraph(body, "{{#if Role in (\"Admin\", \"Editor\")}}");
            AddParagraph(body, "✓ You can publish articles.");
            AddParagraph(body, "{{#else}}");
            AddParagraph(body, "You can read and comment on articles.");
            AddParagraph(body, "{{/if}}");
            AddParagraph(body, "{{#if Country in SupportedCountries}}");
            AddParagraph(body, "✓ Local support is available in {{Country}}.");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // String checks
            AddParagraph(body, "Notices (contains / startswith):", isBold: true);
            AddParagraph(body, "{{#if Remarks contains \"urgent\"}}");
            AddParagraph(body, "⚠ This review requires urgent attention.");
            AddParagraph(body, "{{/if}}");
            AddParagraph(body, "{{#if MemberId startswith \"EU-\"}}");
            AddParagraph(body, "Your data is stored in the EU region.");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Existence checks
            AddParagraph(body, "Profile (exists / is empty):", isBold: true);
            AddParagraph(body, "{{#if Nickname exists}}");
            AddParagraph(body, "A nickname field is present in your profile.");
            AddParagraph(body, "{{/if}}");
            AddParagraph(body, "{{#if Nickname is empty}}");
            AddParagraph(body, "You have not set a nickname yet.");
            AddParagraph(body, "{{/if}}");
            AddParagraph(body, "{{#if Interests is not empty}}");
            AddParagraph(body, "Your interests: {{InterestsSummary}}");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Grouping with parentheses
            AddParagraph(body, "Offers (grouping):", isBold: true);
            AddParagraph(body, "{{#if (Points >= 500 or IsPartner) and not IsSuspended}}");
            AddParagraph(body, "🎁 You qualify for the partner lounge.");
            AddParagraph(body, "{{#else}}");
            AddParagraph(body, "Collect more points to unlock the partner lounge.");
            AddParagraph(body, "{{/if}}");

            doc.Save();
        }

        return templatePath;
    }

    public override Dictionary<string, object> GetSampleData()
    {
        return new Dictionary<string, object>
        {
            ["MemberName"] = "Maria Schneider",
            ["MemberId"] = "EU-20931",
            ["ReviewDate"] = ExampleGenerators.SampleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

            // elseif: 640 points -> Silver
            ["Points"] = 640,

            // in
            ["Role"] = "Editor",
            ["Country"] = "Germany",
            ["SupportedCountries"] = new List<string> { "Austria", "Germany", "Switzerland" },

            // contains / startswith
            ["Remarks"] = "Customer asked for an urgent callback",

            // exists / is empty
            ["Nickname"] = "",
            ["Interests"] = new List<string> { "Hiking", "Photography" },
            ["InterestsSummary"] = "Hiking, Photography",

            // grouping
            ["IsPartner"] = false,
            ["IsSuspended"] = false
        };
    }
}
