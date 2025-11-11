using System.Globalization;
using TriasDev.Templify.Formatting;

namespace TriasDev.Templify.Core;

/// <summary>
/// Configuration options for placeholder replacement in document templates.
/// </summary>
public sealed class PlaceholderReplacementOptions
{
    /// <summary>
    /// Gets or initializes the behavior for handling missing variables.
    /// Default is <see cref="MissingVariableBehavior.LeaveUnchanged"/>.
    /// </summary>
    public MissingVariableBehavior MissingVariableBehavior { get; init; } = MissingVariableBehavior.LeaveUnchanged;

    /// <summary>
    /// Gets or initializes the culture used for formatting numbers, dates, and other culture-sensitive values.
    /// Default is <see cref="CultureInfo.CurrentCulture"/>.
    /// Use <see cref="CultureInfo.InvariantCulture"/> for culture-independent formatting.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// Gets or initializes the boolean formatter registry for custom boolean display formats.
    /// If null, a default registry with culture-aware formatters will be created automatically.
    /// </summary>
    public BooleanFormatterRegistry? BooleanFormatterRegistry { get; init; }

    /// <summary>
    /// Creates a new instance of <see cref="PlaceholderReplacementOptions"/> with default settings.
    /// </summary>
    public PlaceholderReplacementOptions()
    {
    }
}
