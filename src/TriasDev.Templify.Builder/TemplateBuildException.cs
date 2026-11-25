// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// Thrown by <see cref="TemplateBuilder.Build"/> when a template definition is invalid. It lists every problem
/// found, not only the first one.
/// </summary>
/// <remarks>
/// An invalid definition is a programming error, so it is reported by an exception rather than a result object.
/// </remarks>
public sealed class TemplateBuildException : Exception
{
    /// <summary>Initializes a new instance with the problems found.</summary>
    /// <param name="errors">The problems found; at least one.</param>
    public TemplateBuildException(IReadOnlyList<BuildDiagnostic> errors)
        : base(CreateMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>Gets every problem found in the definition.</summary>
    public IReadOnlyList<BuildDiagnostic> Errors { get; }

    private static string CreateMessage(IReadOnlyList<BuildDiagnostic> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        string count = errors.Count == 1 ? "1 error" : $"{errors.Count} errors";
        return $"The template definition is invalid ({count}):" + Environment.NewLine
            + string.Join(Environment.NewLine, errors.Select(e => "  " + e));
    }
}
