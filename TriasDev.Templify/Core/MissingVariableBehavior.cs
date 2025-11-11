namespace TriasDev.Templify.Core;

/// <summary>
/// Defines how to handle placeholders that don't have corresponding values in the data dictionary.
/// </summary>
public enum MissingVariableBehavior
{
    /// <summary>
    /// Leave the placeholder unchanged in the document (e.g., {{VariableName}} remains as-is).
    /// This is the default behavior.
    /// </summary>
    LeaveUnchanged,

    /// <summary>
    /// Replace the placeholder with an empty string (effectively removing it from the document).
    /// </summary>
    ReplaceWithEmpty,

    /// <summary>
    /// Throw an exception when a placeholder without a corresponding value is encountered.
    /// Use this for strict validation scenarios.
    /// </summary>
    ThrowException
}
