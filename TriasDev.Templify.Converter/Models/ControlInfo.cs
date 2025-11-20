namespace TriasDev.Templify.Converter.Models;

/// <summary>
/// Represents information about a single content control in the template.
/// </summary>
public class ControlInfo
{
    /// <summary>
    /// The tag value from the content control (e.g., "variable_process.name").
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// The type of control (Variable, Conditional, Repeating).
    /// </summary>
    public ControlType Type { get; set; }

    /// <summary>
    /// The variable path extracted from the tag (e.g., "process.name").
    /// </summary>
    public string VariablePath { get; set; } = string.Empty;

    /// <summary>
    /// For conditionals, the operator used (eq, ne, gt, lt, and, or, not).
    /// </summary>
    public List<string> Operators { get; set; } = new();

    /// <summary>
    /// For conditionals, the values being compared.
    /// </summary>
    public List<string> ComparisonValues { get; set; } = new();

    /// <summary>
    /// Whether this control contains nested controls.
    /// </summary>
    public bool HasNestedControls { get; set; }

    /// <summary>
    /// Whether this control is inside a table.
    /// </summary>
    public bool InTable { get; set; }

    /// <summary>
    /// Whether this control is inside a table row.
    /// </summary>
    public bool InTableRow { get; set; }

    /// <summary>
    /// The location in the document (paragraph index, section, etc.).
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// The suggested Templify syntax for this control.
    /// </summary>
    public string TemplifySyntax { get; set; } = string.Empty;

    /// <summary>
    /// Whether this control requires manual review.
    /// </summary>
    public bool RequiresManualReview { get; set; }

    /// <summary>
    /// Notes about potential conversion issues.
    /// </summary>
    public List<string> Notes { get; set; } = new();
}

/// <summary>
/// The type of content control.
/// </summary>
public enum ControlType
{
    Unknown,
    Variable,
    Conditional,
    Repeating
}
