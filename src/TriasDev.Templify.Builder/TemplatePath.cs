// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A path into the data a template is filled with, for example <c>customer.address.city</c>. Paths are values:
/// they are built and checked in code and written into the template as Templify placeholders.
/// </summary>
/// <remarks>
/// Start from <see cref="TemplateComposer.Data"/> (or <see cref="Root"/>) and append segments with the indexer:
/// <c>template.Data["customer"]["address.city"]</c>. Each segment consists of letters, digits and <c>_</c>, as
/// Templify's placeholder grammar requires. An invalid segment throws <see cref="ArgumentException"/> where the path is
/// written, so the stack trace points at the line in the definition.
/// </remarks>
public readonly partial struct TemplatePath : IEquatable<TemplatePath>
{
    private readonly string? _value;

    private TemplatePath(string value)
    {
        _value = value;
    }

    /// <summary>Gets a value indicating whether this is the empty path (the root of the data).</summary>
    internal bool IsEmpty => _value is null;

    /// <summary>
    /// Appends one or more dot-separated segments, for example <c>path["address.city"]</c>.
    /// </summary>
    /// <param name="relative">The segments to append, separated by dots.</param>
    /// <exception cref="ArgumentException">A segment is empty or contains characters other than letters, digits and <c>_</c>.</exception>
    public TemplatePath this[string relative]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(relative);
            string[] segments = relative.Split('.');
            foreach (string segment in segments)
            {
                ValidateSegment(segment, relative);
            }

            if (_value is null && string.Equals(segments[0], "this", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "'this' cannot start a data path: Templify reads {{this}} as the current loop item.",
                    nameof(relative));
            }

            return new TemplatePath(_value is null ? relative : _value + "." + relative);
        }
    }

    /// <summary>
    /// Creates a path from the root of the data, for example <c>TemplatePath.Root("customer.name")</c>.
    /// </summary>
    /// <param name="path">The dot-separated segments of the path.</param>
    /// <returns>The path.</returns>
    public static TemplatePath Root(string path) => default(TemplatePath)[path];

    /// <summary>Returns the path as Templify writes it, for example <c>customer.address.city</c>.</summary>
    /// <returns>The dotted path; empty for the root.</returns>
    public override string ToString() => _value ?? string.Empty;

    /// <inheritdoc />
    public bool Equals(TemplatePath other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TemplatePath other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>Compares two paths.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><see langword="true"/> when both paths are equal.</returns>
    public static bool operator ==(TemplatePath left, TemplatePath right) => left.Equals(right);

    /// <summary>Compares two paths.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><see langword="true"/> when the paths differ.</returns>
    public static bool operator !=(TemplatePath left, TemplatePath right) => !left.Equals(right);

    private static void ValidateSegment(string segment, string relative)
    {
        if (!SegmentPattern().IsMatch(segment))
        {
            throw new ArgumentException(
                $"'{relative}' is not a valid data path: each dot-separated segment must consist of letters, digits and '_' (offending segment: '{segment}').",
                nameof(relative));
        }
    }

    // Templify's placeholder grammar (PlaceholderScanner) accepts \w characters in names.
    [GeneratedRegex(@"^\w+$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
