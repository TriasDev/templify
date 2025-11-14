using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Utility class for preserving and applying text formatting (character and paragraph styles).
/// </summary>
internal static class FormattingPreserver
{
    /// <summary>
    /// Extracts RunProperties from a collection of runs.
    /// Returns properties from the first run that has any, or null if none found.
    /// </summary>
    public static RunProperties? ExtractRunProperties(IEnumerable<Run> runs)
    {
        foreach (Run run in runs)
        {
            if (run.RunProperties != null)
            {
                return run.RunProperties;
            }
        }

        return null;
    }

    /// <summary>
    /// Clones RunProperties for use in a new run.
    /// Returns null if original properties are null.
    /// </summary>
    public static RunProperties? CloneRunProperties(RunProperties? originalProperties)
    {
        if (originalProperties == null)
        {
            return null;
        }

        return (RunProperties)originalProperties.CloneNode(true);
    }

    /// <summary>
    /// Applies RunProperties to a run.
    /// If properties are null, the run remains without properties.
    /// </summary>
    public static void ApplyRunProperties(Run run, RunProperties? properties)
    {
        if (properties != null)
        {
            run.RunProperties = properties;
        }
    }

    /// <summary>
    /// Extracts and clones RunProperties from a collection of runs.
    /// Returns cloned properties ready to be applied to a new run.
    /// </summary>
    public static RunProperties? ExtractAndCloneRunProperties(IEnumerable<Run> runs)
    {
        RunProperties? original = ExtractRunProperties(runs);
        return CloneRunProperties(original);
    }

    /// <summary>
    /// Applies markdown-style formatting to RunProperties (bold, italic, strikethrough).
    /// Creates new RunProperties if none exist, or merges with existing properties.
    /// </summary>
    /// <param name="baseProperties">The base RunProperties to start with (can be null).</param>
    /// <param name="isBold">Whether to apply bold formatting.</param>
    /// <param name="isItalic">Whether to apply italic formatting.</param>
    /// <param name="isStrikethrough">Whether to apply strikethrough formatting.</param>
    /// <returns>RunProperties with the markdown formatting applied.</returns>
    public static RunProperties? ApplyMarkdownFormatting(
        RunProperties? baseProperties,
        bool isBold,
        bool isItalic,
        bool isStrikethrough)
    {
        // If no formatting needed, return base properties as-is
        if (!isBold && !isItalic && !isStrikethrough)
        {
            return baseProperties;
        }

        // Create new properties if none exist, or clone existing ones
        RunProperties properties = baseProperties != null
            ? (RunProperties)baseProperties.CloneNode(true)
            : new RunProperties();

        // Apply bold formatting
        if (isBold)
        {
            // Remove existing Bold element if present to avoid duplicates
            properties.RemoveAllChildren<Bold>();
            properties.Append(new Bold());
        }

        // Apply italic formatting
        if (isItalic)
        {
            // Remove existing Italic element if present to avoid duplicates
            properties.RemoveAllChildren<Italic>();
            properties.Append(new Italic());
        }

        // Apply strikethrough formatting
        if (isStrikethrough)
        {
            // Remove existing Strike element if present to avoid duplicates
            properties.RemoveAllChildren<Strike>();
            properties.Append(new Strike());
        }

        return properties;
    }
}
