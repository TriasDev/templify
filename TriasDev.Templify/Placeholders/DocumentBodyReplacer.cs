using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Handles placeholder replacement in document body paragraphs.
/// </summary>
internal sealed class DocumentBodyReplacer
{
    private readonly PlaceholderFinder _finder;
    private readonly ValueResolver _valueResolver;

    public DocumentBodyReplacer(PlaceholderFinder finder, ValueResolver valueResolver)
    {
        _finder = finder ?? throw new ArgumentNullException(nameof(finder));
        _valueResolver = valueResolver ?? throw new ArgumentNullException(nameof(valueResolver));
    }

    /// <summary>
    /// Replaces placeholders in the document body.
    /// </summary>
    /// <param name="document">The Word document to process.</param>
    /// <param name="data">The data dictionary containing replacement values.</param>
    /// <param name="options">Processing options.</param>
    /// <param name="missingVariables">Collection to track missing variables.</param>
    /// <returns>The number of replacements made.</returns>
    public int ReplaceInBody(
        WordprocessingDocument document,
        Dictionary<string, object> data,
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables)
    {
        if (document.MainDocumentPart?.Document?.Body == null)
        {
            return 0;
        }

        int replacementCount = 0;
        Body body = document.MainDocumentPart.Document.Body;

        // Process all paragraphs in the body
        IEnumerable<Paragraph> paragraphs = body.Descendants<Paragraph>();

        foreach (Paragraph paragraph in paragraphs)
        {
            replacementCount += ProcessParagraph(paragraph, data, options, missingVariables);
        }

        return replacementCount;
    }

    /// <summary>
    /// Processes a single paragraph, replacing placeholders in its text.
    /// </summary>
    internal int ProcessParagraph(
        Paragraph paragraph,
        Dictionary<string, object> data,
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables)
    {
        // Get all text runs in the paragraph
        List<Run> runs = paragraph.Descendants<Run>().ToList();

        if (runs.Count == 0)
        {
            return 0;
        }

        // Concatenate all text from runs
        string fullText = string.Concat(runs.Select(r => r.InnerText));

        // Find all placeholders
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(fullText).ToList();

        if (matches.Count == 0)
        {
            return 0;
        }

        // Replace placeholders in the full text
        string replacedText = fullText;
        int replacementCount = 0;

        // Process matches in reverse order to maintain correct indices
        foreach (PlaceholderMatch match in matches.OrderByDescending(m => m.StartIndex))
        {
            if (_valueResolver.TryResolveValue(data, match.VariableName, out object? value))
            {
                string replacementValue = ValueConverter.ConvertToString(value, options.Culture);
                replacedText = replacedText.Remove(match.StartIndex, match.Length)
                                          .Insert(match.StartIndex, replacementValue);
                replacementCount++;
            }
            else
            {
                // Handle missing variable based on options
                missingVariables.Add(match.VariableName);

                switch (options.MissingVariableBehavior)
                {
                    case MissingVariableBehavior.ReplaceWithEmpty:
                        replacedText = replacedText.Remove(match.StartIndex, match.Length);
                        replacementCount++;
                        break;

                    case MissingVariableBehavior.ThrowException:
                        throw new InvalidOperationException($"Missing variable: {match.VariableName}");

                    case MissingVariableBehavior.LeaveUnchanged:
                    default:
                        // Leave the placeholder as-is
                        break;
                }
            }
        }

        // If text changed, update the paragraph
        if (replacedText != fullText)
        {
            UpdateParagraphText(paragraph, runs, replacedText);
        }

        return replacementCount;
    }

    /// <summary>
    /// Updates the paragraph text by removing old runs and creating a new run with the replaced text.
    /// Preserves formatting (RunProperties) from the original runs.
    /// </summary>
    private static void UpdateParagraphText(Paragraph paragraph, List<Run> runs, string newText)
    {
        // Extract and clone formatting from the original runs before removing them
        RunProperties? clonedProperties = FormattingPreserver.ExtractAndCloneRunProperties(runs);

        // Remove all existing runs
        foreach (Run run in runs)
        {
            run.Remove();
        }

        // Create a new run with the replaced text
        Text text = new Text(newText);
        text.Space = SpaceProcessingModeValues.Preserve;
        Run newRun = new Run(text);

        // Apply the preserved formatting to the new run
        FormattingPreserver.ApplyRunProperties(newRun, clonedProperties);

        // Insert the new run at the beginning of the paragraph
        paragraph.AppendChild(newRun);
    }
}
