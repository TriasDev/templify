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
}
