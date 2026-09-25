// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using TriasDev.Templify.Converter.Models;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// A parsed OpenXMLTemplates content-control tag (<c>variable_*</c>, <c>conditionalRemove_*</c>,
/// <c>repeating_*</c>). This is the single place where tags are interpreted; both the analyzer and
/// the converter use it so their output cannot drift apart.
/// </summary>
public sealed class OpenXmlTemplatesTag
{
    /// <summary>Tag prefix of variable controls.</summary>
    public const string VariablePrefix = "variable_";

    /// <summary>Tag prefix of conditional controls.</summary>
    public const string ConditionalPrefix = "conditionalRemove_";

    /// <summary>Tag prefix of repeating controls.</summary>
    public const string RepeatingPrefix = "repeating_";

    private static readonly Regex _collectionPathPattern = new Regex(
        @"^[\w.]+$", RegexOptions.CultureInvariant);

    private static readonly PlaceholderFinder _placeholderFinder = new PlaceholderFinder();

    private OpenXmlTemplatesTag(string tag, ControlType type)
    {
        Tag = tag;
        Type = type;
    }

    /// <summary>The original tag value.</summary>
    public string Tag { get; }

    /// <summary>The kind of control this tag describes.</summary>
    public ControlType Type { get; }

    /// <summary>The variable (or collection) path; for conditionals, the first operand.</summary>
    public string VariablePath { get; private set; } = string.Empty;

    /// <summary>The Templify syntax this tag converts to (null when it cannot be converted).</summary>
    public string? TemplifySyntax { get; private set; }

    /// <summary>For conditionals: the generated Templify condition expression.</summary>
    public ConditionBuildResult? Condition { get; private set; }

    /// <summary>Reasons why the tag cannot be converted automatically.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Notes about conversions that are valid but should be reviewed by a human.</summary>
    public List<string> ReviewNotes { get; } = new();

    /// <summary>True when the tag can be converted to valid Templify syntax.</summary>
    public bool IsConvertible => Type != ControlType.Unknown && Errors.Count == 0;

    /// <summary>
    /// Returns true when the tag uses one of the known OpenXMLTemplates prefixes (case-insensitive,
    /// like OpenXMLTemplates itself).
    /// </summary>
    public static bool IsOpenXmlTemplatesTag(string? tag)
    {
        return tag != null && DetermineType(tag) != ControlType.Unknown;
    }

    /// <summary>
    /// Determine the control type from a tag's prefix.
    /// </summary>
    public static ControlType DetermineType(string tag)
    {
        if (tag.StartsWith(VariablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ControlType.Variable;
        }

        if (tag.StartsWith(ConditionalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ControlType.Conditional;
        }

        if (tag.StartsWith(RepeatingPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ControlType.Repeating;
        }

        return ControlType.Unknown;
    }

    /// <summary>
    /// Parse a tag.
    /// </summary>
    public static OpenXmlTemplatesTag Parse(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        OpenXmlTemplatesTag result = new OpenXmlTemplatesTag(tag, DetermineType(tag));
        switch (result.Type)
        {
            case ControlType.Variable:
                result.ParseVariable(tag.Substring(VariablePrefix.Length));
                break;
            case ControlType.Conditional:
                result.ParseConditional(tag.Substring(ConditionalPrefix.Length));
                break;
            case ControlType.Repeating:
                result.ParseRepeating(tag.Substring(RepeatingPrefix.Length));
                break;
            default:
                result.Errors.Add($"Unknown control tag '{tag}' (not an OpenXMLTemplates tag)");
                break;
        }

        return result;
    }

    private void ParseVariable(string path)
    {
        VariablePath = path;
        string placeholder = "{{" + path + "}}";

        if (path.Length == 0 || !_placeholderFinder.IsValidPlaceholder(placeholder))
        {
            Errors.Add($"'{path}' is not a valid Templify placeholder path");
            return;
        }

        AddUnderscoreNote(path);
        TemplifySyntax = placeholder;
    }

    private void ParseConditional(string remainder)
    {
        ConditionBuildResult condition = ConditionBuilder.Build(remainder);
        Condition = condition;
        VariablePath = condition.VariablePath;
        Errors.AddRange(condition.Errors);
        ReviewNotes.AddRange(condition.ReviewNotes);

        if (condition.Expression != null && condition.Errors.Count == 0)
        {
            TemplifySyntax = $"{{{{#if {condition.Expression}}}}}...{{{{/if}}}}";
        }
    }

    private void ParseRepeating(string remainder)
    {
        string path = remainder;

        // OpenXMLTemplates supports "_separator_<text>" and "_lastSeparator_<text>" arguments.
        // Templify has no equivalent, so they are dropped and reported for review.
        int separatorIndex = IndexOfArgument(remainder);
        if (separatorIndex >= 0)
        {
            path = remainder.Substring(0, separatorIndex);
            ReviewNotes.Add($"Separator arguments '{remainder.Substring(separatorIndex + 1)}' are not supported by Templify and were dropped");
        }

        VariablePath = path;

        if (path.Length == 0 || !_collectionPathPattern.IsMatch(path))
        {
            Errors.Add($"'{path}' is not a valid Templify collection path");
            return;
        }

        AddUnderscoreNote(path);
        TemplifySyntax = $"{{{{#foreach {path}}}}}...{{{{/foreach}}}}";
    }

    private static int IndexOfArgument(string remainder)
    {
        int best = -1;
        foreach (string argument in new[] { "_separator_", "_lastSeparator_" })
        {
            int index = remainder.IndexOf(argument, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }

        return best;
    }

    private void AddUnderscoreNote(string path)
    {
        if (path.Contains('_'))
        {
            string firstSegment = path.Substring(0, path.IndexOf('_'));
            ReviewNotes.Add(
                $"Name '{path}' contains '_'; it was kept intact, but OpenXMLTemplates splits tags on '_' and would have read '{firstSegment}'");
        }
    }
}
