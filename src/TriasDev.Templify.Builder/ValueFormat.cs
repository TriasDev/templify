// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder;

/// <summary>
/// How Templify formats a value: the format specifier of a placeholder, for example <c>{{order.date:date:yyyy-MM-dd}}</c>.
/// </summary>
/// <remarks>
/// A closed set that mirrors the engine's specifiers, so a template can only carry formats the engine understands.
/// <see cref="Custom"/> is the escape hatch for a specifier the set does not model, such as a boolean formatter a
/// product registered in <c>BooleanFormatterRegistry</c>.
/// </remarks>
public sealed partial class ValueFormat
{
    private ValueFormat(string specifier)
    {
        Specifier = specifier;
    }

    /// <summary>Gets the specifier as written after the path's colon, for example <c>date:yyyy-MM-dd</c>.</summary>
    public string Specifier { get; }

    /// <summary>Gets the currency format of the fill culture: <c>:currency</c>.</summary>
    public static ValueFormat Currency { get; } = new ValueFormat("currency");

    /// <summary>Gets upper-case text: <c>:uppercase</c>.</summary>
    public static ValueFormat Uppercase { get; } = new ValueFormat("uppercase");

    /// <summary>Gets lower-case text: <c>:lowercase</c>.</summary>
    public static ValueFormat Lowercase { get; } = new ValueFormat("lowercase");

    /// <summary>Gets the value as is, without markdown: <c>:raw</c>.</summary>
    public static ValueFormat Raw { get; } = new ValueFormat("raw");

    /// <summary>Gets yes / no in the fill culture's language: <c>:yesno</c>.</summary>
    public static ValueFormat YesNo { get; } = new ValueFormat("yesno");

    /// <summary>Gets true / false in the fill culture's language: <c>:truefalse</c>.</summary>
    public static ValueFormat TrueFalse { get; } = new ValueFormat("truefalse");

    /// <summary>Gets on / off in the fill culture's language: <c>:onoff</c>.</summary>
    public static ValueFormat OnOff { get; } = new ValueFormat("onoff");

    /// <summary>Gets enabled / disabled in the fill culture's language: <c>:enabled</c>.</summary>
    public static ValueFormat Enabled { get; } = new ValueFormat("enabled");

    /// <summary>Gets active / inactive in the fill culture's language: <c>:active</c>.</summary>
    public static ValueFormat Active { get; } = new ValueFormat("active");

    /// <summary>Gets ☑ / ☐: <c>:checkbox</c>.</summary>
    public static ValueFormat Checkbox { get; } = new ValueFormat("checkbox");

    /// <summary>Gets ✓ / ✗: <c>:checkmark</c>.</summary>
    public static ValueFormat Checkmark { get; } = new ValueFormat("checkmark");

    /// <summary>A date in a .NET date pattern: <c>:date:yyyy-MM-dd</c>.</summary>
    /// <param name="pattern">The pattern, for example <c>yyyy-MM-dd</c> or <c>MMMM d, yyyy</c>.</param>
    /// <returns>The format.</returns>
    public static ValueFormat Date(string pattern) => new ValueFormat("date:" + Argument(pattern, nameof(pattern)));

    /// <summary>A number in a .NET numeric pattern: <c>:number:N2</c>.</summary>
    /// <param name="pattern">The pattern, for example <c>N2</c>, <c>F3</c> or <c>P</c>.</param>
    /// <returns>The format.</returns>
    public static ValueFormat Number(string pattern) => new ValueFormat("number:" + Argument(pattern, nameof(pattern)));

    /// <summary>
    /// A collection as one text: <c>:join</c>, <c>:join:Name</c>, <c>:join:Name:"; "</c>. Null and empty items are skipped.
    /// </summary>
    /// <param name="itemPath">The property of each item to join, for example <c>Name</c>; null to join the items themselves.</param>
    /// <param name="separator">The separator; null for the engine's default <c>", "</c>.</param>
    /// <returns>The format.</returns>
    public static ValueFormat Join(string? itemPath = null, string? separator = null)
    {
        StringBuilder specifier = new StringBuilder("join");
        if (itemPath != null)
        {
            if (!ItemPathPattern().IsMatch(itemPath))
            {
                throw new ArgumentException(
                    $"'{itemPath}' is not a valid item path: use dot-separated segments of letters, digits and '_'.",
                    nameof(itemPath));
            }

            specifier.Append(':').Append(itemPath);
        }

        if (separator != null)
        {
            Argument(separator, nameof(separator), allowEmpty: true);
            if (separator.Any(c => c is '“' or '”' or '„' or '‟' or '″'))
            {
                throw new ArgumentException("The separator must not contain typographic quotes: Templify reads them as '\"'.", nameof(separator));
            }

            specifier.Append(":\"").Append(separator.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)).Append('"');
        }

        return new ValueFormat(specifier.ToString());
    }

    /// <summary>
    /// A specifier this set does not model, written as is, for example the name of a boolean formatter registered in
    /// <c>BooleanFormatterRegistry</c>.
    /// </summary>
    /// <param name="specifier">A name of letters, digits and <c>_</c>, optionally followed by <c>:</c> and an argument.</param>
    /// <returns>The format.</returns>
    public static ValueFormat Custom(string specifier)
    {
        ArgumentNullException.ThrowIfNull(specifier);
        int colon = specifier.IndexOf(':', StringComparison.Ordinal);
        string name = colon < 0 ? specifier : specifier[..colon];
        if (!NamePattern().IsMatch(name))
        {
            throw new ArgumentException($"'{specifier}' is not a format specifier: it must start with a name of letters, digits and '_'.", nameof(specifier));
        }

        if (colon >= 0)
        {
            Argument(specifier[(colon + 1)..], nameof(specifier));
        }

        return new ValueFormat(specifier);
    }

    /// <summary>Returns the specifier.</summary>
    /// <returns>The specifier, for example <c>date:yyyy-MM-dd</c>.</returns>
    public override string ToString() => Specifier;

    /// <summary>
    /// Checks a format argument against the placeholder grammar: the engine reads everything up to the first <c>}</c>.
    /// </summary>
    private static string Argument(string value, string parameterName, bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (!allowEmpty && value.Length == 0)
        {
            throw new ArgumentException("The format argument must not be empty.", parameterName);
        }

        if (value.Contains('}', StringComparison.Ordinal) || value.Contains("{{", StringComparison.Ordinal) || value.Any(char.IsControl))
        {
            throw new ArgumentException($"'{value}' cannot be a format argument: it must not contain '}}', '{{{{' or control characters.", parameterName);
        }

        return value;
    }

    [GeneratedRegex(@"^\w+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^\w+(\.\w+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ItemPathPattern();
}
