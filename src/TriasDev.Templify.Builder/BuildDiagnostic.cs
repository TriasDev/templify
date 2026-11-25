// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// One problem found while building a template.
/// </summary>
/// <param name="Code">A stable code for the kind of problem, for example <c>TB1001</c>.</param>
/// <param name="Message">A message that names the problem and where it is.</param>
public sealed record BuildDiagnostic(string Code, string Message)
{
    /// <summary>Gets where the problem is in a JSON definition (a JSON pointer such as <c>/content/3</c>); null for C# definitions.</summary>
    public string? Location { get; init; }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}
